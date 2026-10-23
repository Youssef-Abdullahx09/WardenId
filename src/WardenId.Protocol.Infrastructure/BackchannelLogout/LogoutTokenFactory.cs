using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WardenId.Protocol.Application;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Application.Utilities;
using GetActiveForSigningQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.Query;

namespace WardenId.Protocol.Infrastructure.BackchannelLogout;

/// <summary>
/// OIDC Back-Channel Logout 1.0 §2.4 logout_token (spec D9), signed with the tenant's active ES256 key.
/// It is created in a CAP subscriber, outside any HTTP request, so the issuer slug comes from the signing-key
/// lookup rather than ITenantContext.
/// </summary>
public sealed class LogoutTokenFactory(
    ISender sender,
    ITenantSigningCredentialsFactory credentialsFactory,
    IOptions<ProtocolTokenOptions> tokenOptions,
    TimeProvider timeProvider) : ILogoutTokenFactory
{
    public const string TokenType = "logout+jwt";
    public const string BackchannelLogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    public async Task<Result<string>> CreateAsync(
        Guid tenantId, string clientId, Guid subjectId, Guid sid, CancellationToken cancellationToken)
    {
        var keyResult = await sender.Send(new GetActiveForSigningQuery(tenantId), cancellationToken);
        if (keyResult.IsFailed)
            return Result.Fail<string>(keyResult.Errors);

        var key = keyResult.Value;
        var credentials = credentialsFactory.Create(
            new ActiveSigningKey(key.KeyId, key.Algorithm, key.EncryptedPrivateKey, key.Slug));
        // The ECDsa is disposed below. Signature providers are cached by key thumbprint, so a cached one would
        // hand that disposed ECDsa to the next token signed with the same tenant key.
        credentials.CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false };

        try
        {
            var issuedAt = timeProvider.GetUtcNow().UtcDateTime;
            // No default times: the spec claim set has no nbf, and iat/exp come from the injected clock.
            var token = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(
                new SecurityTokenDescriptor
                {
                    TokenType = TokenType,
                    Issuer = IssuerUri.For(tokenOptions.Value.IssuerBaseUri, key.Slug),
                    Audience = clientId,
                    IssuedAt = issuedAt,
                    Expires = issuedAt + Lifetime,
                    SigningCredentials = credentials,
                    Claims = new Dictionary<string, object>
                    {
                        [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                        [JwtRegisteredClaimNames.Sub] = subjectId.ToString(),
                        [SessionClaimTypes.SessionId] = sid.ToString(),
                        // §2.4: a JSON object whose only member is the back-channel logout event, with an empty object value.
                        [OidcValues.Claims.Events] = new Dictionary<string, object>
                        {
                            [BackchannelLogoutEvent] = new Dictionary<string, object>()
                        }
                    }
                });
            return Result.Ok(token);
        }
        finally
        {
            ((ECDsaSecurityKey)credentials.Key).ECDsa.Dispose();
        }
    }
}
