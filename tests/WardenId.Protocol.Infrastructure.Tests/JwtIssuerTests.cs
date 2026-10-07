using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WardenId.Tenancy.Application.Abstractions;

namespace WardenId.Protocol.Infrastructure.Tests;

public class JwtIssuerTests
{
    private const string IssuerBaseUri = "https://wardenid.test";
    private const string Slug = "acme";

    private static (JwtIssuer Issuer, ActiveSigningKey Key, ECDsa PublicKey, string PublicJwk) CreateSut(
        string issuerBaseUri = IssuerBaseUri)
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKeyPkcs8 = ecdsa.ExportPkcs8PrivateKey();

        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector(SigningKeyProtectionPurposes.PrivateKey);
        var encryptedPrivateKey = protector.Protect(privateKeyPkcs8);

        var key = new ActiveSigningKey(Guid.NewGuid(), "ES256", encryptedPrivateKey, Slug);

        var issuer = new JwtIssuer(
            dataProtectionProvider,
            Options.Create(new ProtocolTokenOptions { IssuerBaseUri = issuerBaseUri }));

        // Only the public component is needed to verify signatures.
        var publicKey = ECDsa.Create();
        publicKey.ImportSubjectPublicKeyInfo(ecdsa.ExportSubjectPublicKeyInfo(), out _);

        // Same shape Es256SigningKeyGenerator stores in SigningKey.PublicKeyJwk.
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        var publicJwk = JsonSerializer.Serialize(new
        {
            kty = "EC",
            crv = "P-256",
            x = Base64UrlEncoder.Encode(parameters.Q.X!),
            y = Base64UrlEncoder.Encode(parameters.Q.Y!),
            alg = "ES256",
            use = "sig",
            kid = key.KeyId.ToString("N")
        });

        return (issuer, key, publicKey, publicJwk);
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
        var (issuer, key, publicKey, _) = CreateSut();
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

        Assert.Equal($"{IssuerBaseUri}/tenants/{Slug}", jwt.Issuer);
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
        var (issuer, key, publicKey, _) = CreateSut();
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

        Assert.Equal($"{IssuerBaseUri}/tenants/{Slug}", jwt.Issuer);
        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal("id", jwt.GetClaim("token_use").Value);
        Assert.Equal("someone@example.com", jwt.GetClaim("email").Value);
        Assert.Equal("true", jwt.GetClaim("email_verified").Value);
        Assert.Equal("someone", jwt.GetClaim("preferred_username").Value);
    }

    [Fact]
    public void Sign_Throws_WhenSigningKeyAlgorithmIsNotES256()
    {
        var (issuer, key, _, _) = CreateSut();
        var nonEs256Key = key with { Algorithm = "RS256" };

        Assert.Throws<NotSupportedException>(() =>
            issuer.IssueAccessToken(nonEs256Key, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void IssueAccessToken_DoesNotDoubleSlashIssuer_WhenBaseUriHasTrailingSlash()
    {
        var (issuer, key, _, _) = CreateSut(IssuerBaseUri + "/");

        var token = issuer.IssueAccessToken(key, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15));

        Assert.Equal($"{IssuerBaseUri}/tenants/{Slug}", new JsonWebTokenHandler().ReadJsonWebToken(token).Issuer);
    }

    [Fact]
    public async Task IssuedToken_ValidatesAgainstPublishedJwksAndDiscoveryIssuer()
    {
        var (issuer, key, _, publicJwk) = CreateSut();
        var token = issuer.IssueAccessToken(key, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15));

        // What GET /tenants/{slug}/jwks serves, and what discovery advertises as "issuer".
        var jwks = new JsonWebKeySet($$"""{"keys":[{{publicJwk}}]}""");
        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = IssuerUri.For(IssuerBaseUri, Slug),
            ValidateAudience = false,
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ClockSkew = TimeSpan.Zero
        };

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, validationParameters);

        Assert.True(result.IsValid, result.Exception?.ToString());
    }
}
