using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WardenId.Tenancy.Application.Abstractions;

namespace WardenId.Protocol.Infrastructure.Tests;

public class JwtIssuerTests
{
    private const string IssuerBaseUri = "https://wardenid.test";

    private static (JwtIssuer Issuer, ActiveSigningKey Key, ECDsa PublicKey) CreateSut()
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKeyPkcs8 = ecdsa.ExportPkcs8PrivateKey();

        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector(SigningKeyProtectionPurposes.PrivateKey);
        var encryptedPrivateKey = protector.Protect(privateKeyPkcs8);

        var key = new ActiveSigningKey(Guid.NewGuid(), "ES256", encryptedPrivateKey);

        var issuer = new JwtIssuer(
            dataProtectionProvider,
            Options.Create(new ProtocolTokenOptions { IssuerBaseUri = IssuerBaseUri }));

        // Only the public component is needed to verify signatures.
        var publicKey = ECDsa.Create();
        publicKey.ImportSubjectPublicKeyInfo(ecdsa.ExportSubjectPublicKeyInfo(), out _);

        return (issuer, key, publicKey);
    }

    private static TokenValidationResult ValidateToken(string token, ECDsa publicKey)
    {
        var handler = new JsonWebTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = new ECDsaSecurityKey(publicKey),
            ClockSkew = TimeSpan.Zero
        };

        return handler.ValidateTokenAsync(token, validationParameters).GetAwaiter().GetResult();
    }

    [Fact]
    public void IssueAccessToken_ProducesAWellFormedSignedTokenWithExpectedClaims()
    {
        var (issuer, key, publicKey) = CreateSut();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var issuedAt = DateTimeOffset.UtcNow;
        var lifetime = TimeSpan.FromMinutes(15);

        var token = issuer.IssueAccessToken(key, tenantId, userId, issuedAt, lifetime);

        var validationResult = ValidateToken(token, publicKey);
        Assert.True(validationResult.IsValid, validationResult.Exception?.ToString());

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.Equal("ES256", jwt.Alg);
        Assert.Equal(key.KeyId.ToString("N"), jwt.Kid);

        Assert.Equal($"{IssuerBaseUri}/tenants/{tenantId}", jwt.Issuer);
        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal(tenantId.ToString(), jwt.GetClaim("tenant_id").Value);
        Assert.Equal("access", jwt.GetClaim("token_use").Value);

        Assert.True(jwt.TryGetPayloadValue<long>(JwtRegisteredClaimNames.Iat, out var iat));
        Assert.True(jwt.TryGetPayloadValue<long>(JwtRegisteredClaimNames.Nbf, out var nbf));
        Assert.True(jwt.TryGetPayloadValue<long>(JwtRegisteredClaimNames.Exp, out var exp));
        Assert.True(iat > 0);
        Assert.True(nbf > 0);
        Assert.True(exp > nbf);
        Assert.Equal((long)issuedAt.Add(lifetime).ToUnixTimeSeconds(), exp);
    }

    [Fact]
    public void IssueIdToken_ProducesAWellFormedSignedTokenWithExpectedClaims()
    {
        var (issuer, key, publicKey) = CreateSut();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var issuedAt = DateTimeOffset.UtcNow;
        var lifetime = TimeSpan.FromMinutes(15);

        var token = issuer.IssueIdToken(
            key, tenantId, userId, "someone@example.com", true, "someone", issuedAt, lifetime);

        var validationResult = ValidateToken(token, publicKey);
        Assert.True(validationResult.IsValid, validationResult.Exception?.ToString());

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.Equal("ES256", jwt.Alg);
        Assert.Equal(key.KeyId.ToString("N"), jwt.Kid);

        Assert.Equal($"{IssuerBaseUri}/tenants/{tenantId}", jwt.Issuer);
        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal("id", jwt.GetClaim("token_use").Value);
        Assert.Equal("someone@example.com", jwt.GetClaim("email").Value);
        Assert.Equal("true", jwt.GetClaim("email_verified").Value);
        Assert.Equal("someone", jwt.GetClaim("preferred_username").Value);
    }

    [Fact]
    public void Sign_Throws_WhenSigningKeyAlgorithmIsNotES256()
    {
        var (issuer, key, _) = CreateSut();
        var nonEs256Key = key with { Algorithm = "RS256" };

        Assert.Throws<NotSupportedException>(() =>
            issuer.IssueAccessToken(nonEs256Key, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)));
    }
}
