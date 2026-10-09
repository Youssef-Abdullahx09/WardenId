using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WardenId.Protocol.Application;
using WardenId.Protocol.Application.Abstractions;
using WardenId.SharedKernel.Application;

namespace WardenId.Protocol.Infrastructure;

/// <summary>
/// Signs the placeholder login page's access/ID tokens with the tenant's ES256 key. Replaced by
/// OpenIddict-issued tokens in the /authorize pass.
/// </summary>
public sealed class JwtIssuer(
    ITenantSigningCredentialsFactory credentialsFactory,
    IOptions<ProtocolTokenOptions> options) : IJwtIssuer
{
    public string IssueAccessToken(ActiveSigningKey key, Guid tenantId, Guid userId, DateTimeOffset issuedAtUtc, TimeSpan lifetime) =>
        Sign(
            key, userId, issuedAtUtc, lifetime,
            extraClaims: new Dictionary<string, object>
            {
                [CustomClaimTypes.TenantId] = tenantId.ToString(),
                [CustomClaimTypes.TokenUse] = TokenUse.Access
            });

    public string IssueIdToken(
        ActiveSigningKey key, Guid tenantId, Guid userId, string email, bool emailVerified, string username,
        DateTimeOffset issuedAtUtc, TimeSpan lifetime) =>
        Sign(
            key, userId, issuedAtUtc, lifetime,
            extraClaims: new Dictionary<string, object>
            {
                ["email"] = email,
                ["email_verified"] = emailVerified,
                ["preferred_username"] = username,
                [CustomClaimTypes.TokenUse] = TokenUse.Id
            });

    private string Sign(
        ActiveSigningKey key, Guid userId, DateTimeOffset issuedAtUtc, TimeSpan lifetime,
        Dictionary<string, object> extraClaims)
    {
        var credentials = credentialsFactory.Create(key);
        try
        {
            var claims = new Dictionary<string, object>(extraClaims)
            {
                [JwtRegisteredClaimNames.Iss] = IssuerUri.For(options.Value.IssuerBaseUri, key.Slug),
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
            ((ECDsaSecurityKey)credentials.Key).ECDsa.Dispose();
        }
    }
}
