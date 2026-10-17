using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WardenId.Audit.Infrastructure.Sealing;
using WardenId.Audit.Infrastructure.Tests.Fakes;

namespace WardenId.Audit.Infrastructure.Tests;

public class Es256CheckpointKeyRotatorTests
{
    private sealed class InMemoryAuditDbContextFactory(string databaseName) : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(databaseName).Options);
    }

    // One provider for signer and rotator, as in the app: both must unprotect the same keys.
    private readonly IDataProtectionProvider _dataProtection = new EphemeralDataProtectionProvider();

    private Es256CheckpointSigner Signer(IDbContextFactory<AuditDbContext> factory) => new(_dataProtection, factory);

    private Es256CheckpointKeyRotator Rotator(IDbContextFactory<AuditDbContext> factory, params FakeAuditCheckpointWitness[] witnesses) =>
        new(_dataProtection, factory, witnesses, NullLogger<Es256CheckpointKeyRotator>.Instance);

    private static AuditEvent RotationEvent(string? previousKid, string newKid) =>
        AuditEvent.Create(
            Guid.CreateVersion7(), AuditChannel.Platform, AuditEventType.AuditCheckpointKeyRotate, 1, DateTimeOffset.UtcNow,
            AuditSeverity.Warning, Actor.Create(ActorType.AdminUser, "admin-1", ActorScope.Platform()).Value,
            AuditOutcome.Success(), ClientContext.Unknown(), AuthContext.None(), Correlation.None(),
            [AuditTarget.Create(AuditTargetType.CheckpointSigningKey, newKid).Value]).Value;

    [Fact]
    public async Task RotateAsync_NewCheckpointsAreSignedWithTheNewKey_TheOldKeyStaysInactive()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RotateAsync_NewCheckpointsAreSignedWithTheNewKey_TheOldKeyStaysInactive));
        var (oldKid, _) = await Signer(factory).SignAsync("before"u8.ToArray());

        var rotation = await Rotator(factory).RotateAsync(RotationEvent);
        var (kidAfter, signatureAfter) = await Signer(factory).SignAsync("after"u8.ToArray());

        Assert.True(rotation.IsSuccess);
        Assert.Equal(oldKid, rotation.Value.PreviousKid);
        Assert.NotEqual(oldKid, rotation.Value.Kid);
        Assert.Equal(rotation.Value.Kid, kidAfter);

        await using var db = factory.CreateDbContext();
        var keys = await db.AuditCheckpointSigningKeys.ToListAsync();
        Assert.Equal(2, keys.Count);
        var oldKey = keys.Single(k => k.Id.ToString("N") == oldKid);
        var newKey = keys.Single(k => k.Id.ToString("N") == kidAfter);
        Assert.False(oldKey.IsActive);
        Assert.NotNull(oldKey.DeactivatedAt);
        Assert.True(newKey.IsActive);
        Assert.True(Verify(newKey.PublicKeyJwk, "after"u8.ToArray(), signatureAfter));
    }

    [Fact]
    public async Task RotateAsync_TheOutgoingKeySignsTheRotationStatement()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RotateAsync_TheOutgoingKeySignsTheRotationStatement));
        var (oldKid, _) = await Signer(factory).SignAsync("before"u8.ToArray());

        var rotation = (await Rotator(factory).RotateAsync(RotationEvent)).Value;

        await using var db = factory.CreateDbContext();
        var keys = await db.AuditCheckpointSigningKeys.ToListAsync();
        var oldKey = keys.Single(k => k.Id.ToString("N") == oldKid);
        var newKey = keys.Single(k => k.Id.ToString("N") == rotation.Kid);
        Assert.Equal(oldKey.Id, newKey.PreviousKeyId);
        var (x, y) = Point(newKey.PublicKeyJwk);
        var statement = CheckpointKeyRotation.BuildUnsignedBody(oldKid, rotation.Kid, x, y);
        Assert.True(Verify(oldKey.PublicKeyJwk, statement, newKey.RotationAttestation!));
    }

    [Fact]
    public async Task RotateAsync_WritesTheAuditEventInTheSameSave_InThePlatformScope()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RotateAsync_WritesTheAuditEventInTheSameSave_InThePlatformScope));
        await Signer(factory).SignAsync("before"u8.ToArray());

        var rotation = (await Rotator(factory).RotateAsync(RotationEvent)).Value;

        await using var db = factory.CreateDbContext();
        var record = await db.AuditEvents.SingleAsync();
        Assert.Equal(nameof(AuditChannel.Platform), record.Channel);
        Assert.Null(record.TenantId);
        Assert.Equal(AuditEventType.AuditCheckpointKeyRotate.Key, record.EventType);
        Assert.Equal([$"checkpoint_signing_key:{rotation.Kid}"], record.Targets!);
    }

    [Fact]
    public async Task RotateAsync_BeforeAnyKeyExists_CreatesTheFirstKeyWithoutAStatement()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RotateAsync_BeforeAnyKeyExists_CreatesTheFirstKeyWithoutAStatement));
        var witness = new FakeAuditCheckpointWitness();

        var rotation = (await Rotator(factory, witness).RotateAsync(RotationEvent)).Value;

        Assert.Null(rotation.PreviousKid);
        await using var db = factory.CreateDbContext();
        var key = await db.AuditCheckpointSigningKeys.SingleAsync();
        Assert.True(key.IsActive);
        Assert.Null(key.PreviousKeyId);
        Assert.Null(key.RotationAttestation);
        Assert.Empty(witness.Witnessed);
    }

    [Fact]
    public async Task RotateAsync_HandsTheSignedStatementToTheWitnesses()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RotateAsync_HandsTheSignedStatementToTheWitnesses));
        var (oldKid, _) = await Signer(factory).SignAsync("before"u8.ToArray());
        var witness = new FakeAuditCheckpointWitness();

        var rotation = (await Rotator(factory, witness).RotateAsync(RotationEvent)).Value;

        var note = Assert.Single(witness.Witnessed);
        Assert.StartsWith($"warden-audit-key-rotation\n{oldKid}\n{rotation.Kid}\n", note);
        Assert.Contains($"\n— {oldKid} ", note);
    }

    [Fact]
    public async Task RotateAsync_AFailingWitness_DoesNotUndoTheRotation()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RotateAsync_AFailingWitness_DoesNotUndoTheRotation));
        await Signer(factory).SignAsync("before"u8.ToArray());

        var rotation = await Rotator(factory, new FakeAuditCheckpointWitness { ThrowOnWitness = true }).RotateAsync(RotationEvent);

        Assert.True(rotation.IsSuccess);
        Assert.Equal(rotation.Value.Kid, (await Signer(factory).SignAsync("after"u8.ToArray())).Kid);
    }

    private static (string X, string Y) Point(string publicKeyJwk)
    {
        using var jwk = JsonDocument.Parse(publicKeyJwk);
        return (jwk.RootElement.GetProperty("x").GetString()!, jwk.RootElement.GetProperty("y").GetString()!);
    }

    private static bool Verify(string publicKeyJwk, byte[] data, byte[] signature)
    {
        var (x, y) = Point(publicKeyJwk);
        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Base64UrlDecode(x), Y = Base64UrlDecode(y) }
        });
        return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
