using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WardenId.Protocol.Application;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Tenancy.Application.Abstractions;

namespace WardenId.Protocol.Infrastructure;

/// <summary>
/// Decrypts a tenant's ES256 signing key (protected by Tenancy with the shared
/// SigningKeyProtectionPurposes.PrivateKey purpose string) and signs access/ID tokens with it.
/// This is the "only ever decrypted inside the Protocol module" consumer referenced in
/// Tenancy's SigningKeyMaterial doc comment.
/// </summary>
public sealed class JwtIssuer(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<ProtocolTokenOptions> options) : IJwtIssuer
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector(SigningKeyProtectionPurposes.PrivateKey);

    public string IssueAccessToken(ActiveSigningKey key, Guid tenantId, Guid userId, DateTimeOffset issuedAtUtc, TimeSpan lifetime) =>
        Sign(
            key, tenantId, userId, issuedAtUtc, lifetime,
            extraClaims: new Dictionary<string, object>
            {
                ["tenant_id"] = tenantId.ToString(),
                ["token_use"] = "access"
            });

    public string IssueIdToken(
        ActiveSigningKey key, Guid tenantId, Guid userId, string email, bool emailVerified, string username,
        DateTimeOffset issuedAtUtc, TimeSpan lifetime) =>
        Sign(
            key, tenantId, userId, issuedAtUtc, lifetime,
            extraClaims: new Dictionary<string, object>
            {
                ["email"] = email,
                ["email_verified"] = emailVerified,
                ["preferred_username"] = username,
                ["token_use"] = "id"
            });

    private string Sign(
        ActiveSigningKey key, Guid tenantId, Guid userId, DateTimeOffset issuedAtUtc, TimeSpan lifetime,
        Dictionary<string, object> extraClaims)
    {
        if (key.Algorithm != "ES256")
            throw new NotSupportedException($"Unsupported signing key algorithm: '{key.Algorithm}'. Only ES256 is supported.");

        var privateKeyPkcs8 = _protector.Unprotect(key.EncryptedPrivateKey);
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);

            var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = key.KeyId.ToString("N") };
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

            var claims = new Dictionary<string, object>(extraClaims)
            {
                [JwtRegisteredClaimNames.Iss] = $"{options.Value.IssuerBaseUri}/tenants/{tenantId}",
                [JwtRegisteredClaimNames.Sub] = userId.ToString()
            };

            var descriptor = new SecurityTokenDescriptor
            {
                Claims = claims,
                IssuedAt = issuedAtUtc.UtcDateTime,
                NotBefore = issuedAtUtc.UtcDateTime,
                Expires = issuedAtUtc.Add(lifetime).UtcDateTime,
                SigningCredentials = credentials
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyPkcs8);
        }
    }
}
