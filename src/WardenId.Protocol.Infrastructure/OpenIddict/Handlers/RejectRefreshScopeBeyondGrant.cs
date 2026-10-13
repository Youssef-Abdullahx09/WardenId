using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// refresh_token grant with a <c>scope</c> beyond the original grant -> <c>invalid_scope</c>
/// (RFC 6749 §5.2/§6). Runs just before OpenIddict's own ValidateGrantedScopes, which rejects the
/// same request with <c>invalid_grant</c> (ID2052). Narrower scopes pass through to the Token endpoint.
/// </summary>
public sealed class RejectRefreshScopeBeyondGrant : IOpenIddictServerHandler<ValidateTokenRequestContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenRequestContext>()
            .UseSingletonHandler<RejectRefreshScopeBeyondGrant>()
            .SetOrder(OpenIddictServerHandlers.Exchange.ValidateGrantedScopes.Descriptor.Order - 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(ValidateTokenRequestContext context)
    {
        if (!context.Request.IsRefreshTokenGrantType() || context.RefreshTokenPrincipal is not { } principal)
            return ValueTask.CompletedTask;

        var requested = context.Request.GetScopes();
        if (!requested.IsDefaultOrEmpty && requested.Except(principal.GetScopes(), StringComparer.Ordinal).Any())
            context.Reject(error: Errors.InvalidScope, description: "The requested scope exceeds the original grant.");

        return ValueTask.CompletedTask;
    }
}
