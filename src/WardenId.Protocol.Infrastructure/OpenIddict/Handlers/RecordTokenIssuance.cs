using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Auditing;
using WardenId.SharedKernel.Application.Abstractions;
using static OpenIddict.Server.OpenIddictServerEvents;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// Audits every token response of the token endpoint, once OpenIddict has generated the tokens: one event per
/// grant type, with the access token's jti, the grant's token family and the user's session. The client is the
/// actor (it redeemed the grant). A rejected sign-in issued nothing and records nothing.
/// </summary>
public sealed class RecordTokenIssuance(IAuthAuditTrail audit, ITenantContext tenantContext, TimeProvider timeProvider)
    : IOpenIddictServerHandler<ProcessSignInContext>
{
    private static readonly JsonWebTokenHandler Jwts = new();

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessSignInContext>()
            .UseScopedHandler<RecordTokenIssuance>()
            // After the tokens and response parameters exist; before the response is written.
            .SetOrder(OpenIddictServerHandlers.AttachSignInParameters.Descriptor.Order + 1_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ProcessSignInContext context)
    {
        if (context.IsRejected
            || context.EndpointType is not OpenIddictServerEndpointType.Token
            || context.Request.ClientId is not { Length: > 0 } clientId
            || tenantContext.TenantId is not { } tenantId)
        {
            return;
        }

        SecurityEvent? securityEvent = context.Request switch
        {
            var r when r.IsAuthorizationCodeGrantType() => SecurityEvent.TokenIssuedAuthorizationCode,
            var r when r.IsRefreshTokenGrantType() => SecurityEvent.TokenIssuedRefreshToken,
            var r when r.IsClientCredentialsGrantType() => SecurityEvent.TokenIssuedClientCredentials,
            _ => null
        };
        if (securityEvent is not { } issued) return;

        var tokenId = JwtIdOf(context.AccessToken) ?? context.AccessTokenPrincipal?.GetTokenId();

        await audit.RecordAsync(new RecordAuditEventCommand(
            issued,
            tenantId,
            timeProvider.GetUtcNow(),
            Actor.Client(clientId),
            // A client_credentials subject is the client itself, not a user.
            issued == SecurityEvent.TokenIssuedClientCredentials
                ? [Target.Client(clientId)]
                : TokenAuditFacts.Targets(clientId, context.Principal),
            Session: TokenAuditFacts.Session(context.Principal, tokenId)));
    }

    // The jti resource servers see (OpenIddict assigns it while generating the JWT). Read, never validated or stored.
    private static string? JwtIdOf(string? accessToken) =>
        accessToken is { Length: > 0 } && Jwts.CanReadToken(accessToken)
            ? Jwts.ReadJsonWebToken(accessToken).Id is { Length: > 0 } jti ? jti : null
            : null;
}
