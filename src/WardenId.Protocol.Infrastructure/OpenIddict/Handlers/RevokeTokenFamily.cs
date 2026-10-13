using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// RFC 7009 §2.1: revoking a refresh token also invalidates the access tokens of the same grant.
/// OpenIddict's RevokeToken revokes only the presented token, so this revokes its authorization and
/// every token under it (the family). Failures propagate (→ 500). If the authorization write fails, RevokeToken has already revoked the presented
/// refresh token, so a retry is a no-op 200 and sibling access tokens stay valid until they expire (≤ 15 min).
/// </summary>
public sealed class RevokeTokenFamily(IOpenIddictAuthorizationManager authorizations, IOpenIddictTokenManager tokens)
    : IOpenIddictServerHandler<HandleRevocationRequestContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<HandleRevocationRequestContext>()
            .UseScopedHandler<RevokeTokenFamily>()
            .SetOrder(OpenIddictServerHandlers.Revocation.RevokeToken.Descriptor.Order + 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(HandleRevocationRequestContext context)
    {
        if (context.IsRejected
            || context.GenericTokenPrincipal is not { } principal
            || !principal.HasTokenType(TokenTypeIdentifiers.RefreshToken)
            || principal.GetAuthorizationId() is not { Length: > 0 } authorizationId)
        {
            return;
        }

        var cancellationToken = context.Transaction.GetHttpRequest()?.HttpContext.RequestAborted ?? CancellationToken.None;

        if (await authorizations.FindByIdAsync(authorizationId, cancellationToken) is { } authorization)
            await authorizations.TryRevokeAsync(authorization, cancellationToken);

        await tokens.RevokeByAuthorizationIdAsync(authorizationId, cancellationToken);
    }
}
