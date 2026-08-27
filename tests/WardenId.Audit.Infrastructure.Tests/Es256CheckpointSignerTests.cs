using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Infrastructure.Sealing;

namespace WardenId.Audit.Infrastructure.Tests;

public class Es256CheckpointSignerTests
{
    private sealed class InMemoryAuditDbContextFactory(string databaseName) : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(databaseName).Options);
    }

    private static Es256CheckpointSigner CreateSut(InMemoryAuditDbContextFactory factory) =>
        new(new EphemeralDataProtectionProvider(), factory);

    [Fact]
    public async Task SignAsync_FirstCall_GeneratesAndPersistsOneActiveKey()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(SignAsync_FirstCall_GeneratesAndPersistsOneActiveKey));
        var sut = CreateSut(factory);

        var (kid, signature) = await sut.SignAsync(Encoding.UTF8.GetBytes("checkpoint body"));

        Assert.NotEmpty(kid);
        Assert.NotEmpty(signature);

        await using var db = factory.CreateDbContext();
        var key = await db.AuditCheckpointSigningKeys.SingleAsync();
        Assert.True(key.IsActive);
        Assert.Equal(kid, key.Id.ToString("N"));
    }

    [Fact]
    public async Task SignAsync_SecondCall_ReusesTheSameActiveKey()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(SignAsync_SecondCall_ReusesTheSameActiveKey));
        var sut = CreateSut(factory);

        var (kid1, _) = await sut.SignAsync(Encoding.UTF8.GetBytes("body one"));
        var (kid2, _) = await sut.SignAsync(Encoding.UTF8.GetBytes("body two"));

        Assert.Equal(kid1, kid2);
        await using var db = factory.CreateDbContext();
        Assert.Equal(1, await db.AuditCheckpointSigningKeys.CountAsync());
    }

    [Fact]
    public async Task SignAsync_SignatureVerifiesAgainstThePublishedPublicKey()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(SignAsync_SignatureVerifiesAgainstThePublishedPublicKey));
        var sut = CreateSut(factory);
        var body = Encoding.UTF8.GetBytes("warden-audit/platform/adminconfig\n1\nabc123==\n");

        var (_, signature) = await sut.SignAsync(body);

        await using var db = factory.CreateDbContext();
        var key = await db.AuditCheckpointSigningKeys.SingleAsync();
        using var jwk = JsonDocument.Parse(key.PublicKeyJwk);
        var x = Base64UrlDecode(jwk.RootElement.GetProperty("x").GetString()!);
        var y = Base64UrlDecode(jwk.RootElement.GetProperty("y").GetString()!);

        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = x, Y = y }
        });

        Assert.True(ecdsa.VerifyData(body, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
