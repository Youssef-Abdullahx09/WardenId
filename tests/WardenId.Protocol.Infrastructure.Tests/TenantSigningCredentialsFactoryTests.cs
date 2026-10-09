using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Tenancy.Application.Abstractions;

namespace WardenId.Protocol.Infrastructure.Tests;

public class TenantSigningCredentialsFactoryTests
{
    private static (TenantSigningCredentialsFactory Factory, ActiveSigningKey Key, ECDsa PublicKey) CreateSut()
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = new EphemeralDataProtectionProvider();
        var encrypted = provider.CreateProtector(SigningKeyProtectionPurposes.PrivateKey).Protect(ecdsa.ExportPkcs8PrivateKey());
        var publicKey = ECDsa.Create();
        publicKey.ImportSubjectPublicKeyInfo(ecdsa.ExportSubjectPublicKeyInfo(), out _);
        return (new TenantSigningCredentialsFactory(provider), new ActiveSigningKey(Guid.NewGuid(), "ES256", encrypted, "acme"), publicKey);
    }

    [Fact]
    public void Create_ReturnsEs256Credentials_WithNFormatKid()
    {
        var (factory, key, _) = CreateSut();

        var credentials = factory.Create(key);

        Assert.Equal(SecurityAlgorithms.EcdsaSha256, credentials.Algorithm);
        Assert.Equal(key.KeyId.ToString("N"), credentials.Key.KeyId);
    }

    [Fact]
    public async Task Create_ProducesCredentialsThatSignVerifiably()
    {
        var (factory, key, publicKey) = CreateSut();
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "i",
            SigningCredentials = factory.Create(key)
        });

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "i",
            ValidateAudience = false,
            IssuerSigningKey = new ECDsaSecurityKey(publicKey)
        });

        Assert.True(result.IsValid, result.Exception?.ToString());
    }

    [Fact]
    public void Create_Throws_ForNonEs256Key()
    {
        var (factory, key, _) = CreateSut();

        Assert.Throws<NotSupportedException>(() => factory.Create(key with { Algorithm = "RS256" }));
    }
}
