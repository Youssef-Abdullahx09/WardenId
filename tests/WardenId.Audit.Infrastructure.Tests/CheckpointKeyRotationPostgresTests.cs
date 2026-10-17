using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WardenId.Audit.Infrastructure.Sealing;
using VerifyHandler = WardenId.Audit.Application.Features.Verification.Verify.Handler;
using VerifyQuery = WardenId.Audit.Application.Features.Verification.Verify.Query;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>
/// Rotation against the real schema: the partial unique index on the active key, and a channel whose
/// checkpoints were signed by the old key and then the new one still verifying end to end.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CheckpointKeyRotationPostgresTests(PostgresFixture postgres)
{
    private static readonly Guid Acme = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly IDataProtectionProvider _dataProtection = new EphemeralDataProtectionProvider();

    private static AuditEvent AdminEvent(AuditChannel channel = AuditChannel.AdminConfig, AuditEventType? type = null, string? targetKid = null) =>
        AuditEvent.Create(
            Guid.CreateVersion7(), channel, type ?? AuditEventType.AdminEntityUpdate, 1, DateTimeOffset.UtcNow, AuditSeverity.Info,
            Actor.Create(ActorType.AdminUser, "admin-1", ActorScope.ForTenant(Acme)).Value,
            AuditOutcome.Success(), ClientContext.Unknown(), AuthContext.None(), Correlation.None(),
            targetKid is null ? null : [AuditTarget.Create(AuditTargetType.CheckpointSigningKey, targetKid).Value]).Value;

    private async Task<NpgsqlAuditDbContextFactory> MigratedAsync()
    {
        var factory = new NpgsqlAuditDbContextFactory(await postgres.CreateDatabaseAsync());
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        return factory;
    }

    private static async Task RecordAsync(NpgsqlAuditDbContextFactory factory, int count)
    {
        var recorder = new AuditRecorder(factory, NullLogger<AuditRecorder>.Instance);
        for (var i = 0; i < count; i++) await recorder.RecordAsync(AdminEvent(), Acme);
    }

    private async Task SealEverythingAsync(NpgsqlAuditDbContextFactory factory)
    {
        var sealer = new AuditSealerService(
            factory,
            new PgSealableEventReader(),
            new PgChannelAdvisoryLock(),
            new Es256CheckpointSigner(_dataProtection, factory),
            [],
            Options.Create(new AuditSealerOptions
            {
                MaxSegmentSize = 2,
                MaxSegmentAgeSeconds = 0,
                MaxSegmentsPerCheckpoint = 1,
                MaxCheckpointIntervalSeconds = 0
            }),
            NullLogger<AuditSealerService>.Instance);

        // Sealing waits for the cluster-wide snapshot watermark (I4); see AuditEventSearchColumnsMigrationTests.
        for (var pass = 0; pass < 150; pass++)
        {
            await sealer.RunPassAsync(CancellationToken.None);
            await using var db = factory.CreateDbContext();
            if (!await db.AuditEvents.AnyAsync(e => e.Sequence == null)) return;
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
        Assert.Fail("The sealer left events unsealed.");
    }

    [SkippableFact]
    public async Task AChannelCheckpointedBeforeAndAfterARotation_StillVerifies()
    {
        Skip.If(postgres.SkipReason is not null, postgres.SkipReason);
        var factory = await MigratedAsync();

        await RecordAsync(factory, 4);
        await SealEverythingAsync(factory);

        var rotator = new Es256CheckpointKeyRotator(_dataProtection, factory, [], NullLogger<Es256CheckpointKeyRotator>.Instance);
        var rotation = await rotator.RotateAsync(
            (_, newKid) => AdminEvent(AuditChannel.Platform, AuditEventType.AuditCheckpointKeyRotate, newKid));
        Assert.True(rotation.IsSuccess, string.Join("; ", rotation.Errors.Select(e => e.Message)));

        await RecordAsync(factory, 4);
        await SealEverythingAsync(factory);

        var repository = new AuditProofRepository(factory);
        var adminConfig = (await new VerifyHandler(repository).Handle(
            new VerifyQuery(AuditChannel.AdminConfig, Acme), CancellationToken.None)).Value;
        Assert.True(adminConfig.Verified);
        Assert.Equal([rotation.Value.PreviousKid!, rotation.Value.Kid], adminConfig.Checkpoints.Select(c => c.Kid).Distinct().ToArray());
        Assert.All(adminConfig.Checkpoints, c => Assert.True(c.SigningKeyKnown && c.SigningKeyContinuous));

        // The rotation itself is chained into the platform log, checkpointed under the new key.
        var platform = (await new VerifyHandler(repository).Handle(
            new VerifyQuery(AuditChannel.Platform, null), CancellationToken.None)).Value;
        Assert.True(platform.Verified);
        Assert.Equal(rotation.Value.Kid, Assert.Single(platform.Checkpoints).Kid);
    }

    [SkippableFact]
    public async Task RotatingTwice_KeepsExactlyOneActiveKey_AndAChainOfStatements()
    {
        Skip.If(postgres.SkipReason is not null, postgres.SkipReason);
        var factory = await MigratedAsync();
        var rotator = new Es256CheckpointKeyRotator(_dataProtection, factory, [], NullLogger<Es256CheckpointKeyRotator>.Instance);

        var first = (await rotator.RotateAsync((_, kid) => AdminEvent(AuditChannel.Platform, AuditEventType.AuditCheckpointKeyRotate, kid))).Value;
        var second = (await rotator.RotateAsync((_, kid) => AdminEvent(AuditChannel.Platform, AuditEventType.AuditCheckpointKeyRotate, kid))).Value;
        var third = (await rotator.RotateAsync((_, kid) => AdminEvent(AuditChannel.Platform, AuditEventType.AuditCheckpointKeyRotate, kid))).Value;

        Assert.Equal(first.Kid, second.PreviousKid);
        Assert.Equal(second.Kid, third.PreviousKid);
        var keys = await new AuditProofRepository(factory).GetCheckpointKeysAsync();
        Assert.Equal(3, keys.Count);
        Assert.Equal(third.Kid, Assert.Single(keys, k => k.IsActive).Kid);
        Assert.Equal(second.Kid, keys.Single(k => k.Kid == third.Kid).PreviousKid);
        Assert.NotNull(keys.Single(k => k.Kid == third.Kid).RotationAttestation);
    }
}
