using System.Security.Cryptography;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WardenId.Protocol.Infrastructure.BackchannelLogout;
using WardenId.Protocol.Infrastructure.Tests.Fakes;
using WardenId.Tenancy.Application.Abstractions;
using GetActiveForSigningQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.Query;
using TenancyActiveSigningKey = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.ActiveSigningKey;

namespace WardenId.Protocol.Infrastructure.Tests.BackchannelLogout;

public class LogoutTokenFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();
    private static readonly Guid Sid = Guid.NewGuid();
    private const string Issuer = "https://wardenid.test/tenants/acme";

    private sealed record Sut(LogoutTokenFactory Factory, StubSender Sender, TenancyActiveSigningKey Key, SecurityKey PublicKey);

    private static Sut CreateSut(Result<TenancyActiveSigningKey>? keyResponse = null)
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var protection = new EphemeralDataProtectionProvider();
        var key = new TenancyActiveSigningKey(
            Guid.NewGuid(), "ES256",
            protection.CreateProtector(SigningKeyProtectionPurposes.PrivateKey).Protect(ecdsa.ExportPkcs8PrivateKey()),
            "acme");
        var publicKey = ECDsa.Create();
        publicKey.ImportSubjectPublicKeyInfo(ecdsa.ExportSubjectPublicKeyInfo(), out _);

        var sender = new StubSender()
            .Returns<GetActiveForSigningQuery, Result<TenancyActiveSigningKey>>(keyResponse ?? Result.Ok(key));
        var factory = new LogoutTokenFactory(
            sender,
            new TenantSigningCredentialsFactory(protection),
            Options.Create(new ProtocolTokenOptions { IssuerBaseUri = "https://wardenid.test" }),
            new FakeTimeProvider(Now));
        return new Sut(factory, sender, key, new ECDsaSecurityKey(publicKey));
    }

    private static Task<TokenValidationResult> ValidateAsync(string token, SecurityKey publicKey) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = "web",
            ValidTypes = ["logout+jwt"],
            IssuerSigningKey = publicKey,
            // Issued on the FakeTimeProvider clock, not wall time; exp is asserted separately below.
            ValidateLifetime = false
        });

    private static async Task<JsonWebToken> CreateTokenAsync(Sut sut)
    {
        var result = await sut.Factory.CreateAsync(TenantId, "web", SubjectId, Sid, CancellationToken.None);
        Assert.True(result.IsSuccess);
        return new JsonWebToken(result.Value);
    }

    [Fact]
    public async Task CreateAsync_SignsWithTheTenantKey_AsLogoutJwt()
    {
        var sut = CreateSut();

        var token = await CreateTokenAsync(sut);
        var validation = await ValidateAsync(token.EncodedToken, sut.PublicKey);

        Assert.True(validation.IsValid, validation.Exception?.ToString());
        Assert.Equal("logout+jwt", token.Typ);
        Assert.Equal("ES256", token.Alg);
        Assert.Equal(sut.Key.KeyId.ToString("N"), token.Kid);
    }

    [Fact]
    public async Task CreateAsync_LooksUpTheEventsTenantKey()
    {
        var sut = CreateSut();

        await CreateTokenAsync(sut);

        Assert.Equal(new GetActiveForSigningQuery(TenantId), Assert.Single(sut.Sender.Sent));
    }

    [Fact]
    public async Task CreateAsync_SetsLogoutTokenClaims_WithoutNonce()
    {
        var token = await CreateTokenAsync(CreateSut());

        Assert.Equal(Issuer, token.Issuer);
        Assert.Equal("web", Assert.Single(token.Audiences));
        Assert.Equal(SubjectId.ToString(), token.Subject);
        Assert.Equal(Sid.ToString(), token.GetPayloadValue<string>("sid"));
        Assert.True(Guid.TryParse(token.Id, out _));
        Assert.False(token.TryGetPayloadValue<string>("nonce", out _));
    }

    [Fact]
    public async Task CreateAsync_EventsClaim_IsTheBackchannelLogoutObject()
    {
        var token = await CreateTokenAsync(CreateSut());

        Assert.True(token.TryGetPayloadValue<JsonElement>("events", out var events));
        Assert.Equal(JsonValueKind.Object, events.ValueKind);
        var logoutEvent = Assert.Single(events.EnumerateObject());
        Assert.Equal("http://schemas.openid.net/event/backchannel-logout", logoutEvent.Name);
        Assert.Equal(JsonValueKind.Object, logoutEvent.Value.ValueKind);
        Assert.Empty(logoutEvent.Value.EnumerateObject());
    }

    [Fact]
    public async Task CreateAsync_ExpiresTwoMinutesAfterIssuedAt()
    {
        var token = await CreateTokenAsync(CreateSut());

        var issuedAt = token.GetPayloadValue<long>("iat");
        Assert.Equal(Now.ToUnixTimeSeconds(), issuedAt);
        Assert.Equal(120, token.GetPayloadValue<long>("exp") - issuedAt);
    }

    [Fact]
    public async Task CreateAsync_Twice_WithTheSameTenantKey_BothVerify()
    {
        // The factory disposes each ECDsa after signing; a cached signature provider would reuse a disposed key.
        var sut = CreateSut();

        var first = await CreateTokenAsync(sut);
        var second = await CreateTokenAsync(sut);

        Assert.True((await ValidateAsync(first.EncodedToken, sut.PublicKey)).IsValid);
        Assert.True((await ValidateAsync(second.EncodedToken, sut.PublicKey)).IsValid);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenTheTenantHasNoActiveSigningKey()
    {
        var sut = CreateSut(Result.Fail<TenancyActiveSigningKey>("No active signing key."));

        var result = await sut.Factory.CreateAsync(TenantId, "web", SubjectId, Sid, CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
