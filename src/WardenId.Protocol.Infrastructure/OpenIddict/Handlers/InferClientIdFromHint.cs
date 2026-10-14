using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// A4. Without client_id, OpenIddict validates post_logout_redirect_uri by searching every client
/// (FindByPostLogoutRedirectUriAsync), which the read-only, tenant-scoped WardenApplicationStore can't
/// answer, so the URI would always be rejected. When client_id is absent but a valid id_token_hint names
/// exactly one client (presenter, else audience), that client becomes the client_id. OpenIddict's
/// client-bound checks then run as if the RP had sent it: exact post_logout_redirect_uri,
/// ept:end_session, and the hint's aud/presenter.
/// </summary>
public sealed class InferClientIdFromHint(IOpenIddictApplicationManager applications)
    : IOpenIddictServerHandler<ValidateEndSessionRequestContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateEndSessionRequestContext>()
            .UseScopedHandler<InferClientIdFromHint>()
            .SetOrder(OpenIddictServerHandlers.Session.ValidateAuthentication.Descriptor.Order + 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ValidateEndSessionRequestContext context)
    {
        // Only a post_logout_redirect_uri needs a client to be validated against; without one there is
        // nothing to infer for.
        if (context.IsRejected
            || !string.IsNullOrEmpty(context.ClientId)
            || string.IsNullOrEmpty(context.PostLogoutRedirectUri)
            || context.IdentityTokenHintPrincipal is not { } hint)
        {
            return;
        }

        var presenters = hint.GetPresenters();
        var audiences = hint.GetAudiences();
        var clientId = presenters.Length == 1 ? presenters[0]
            : audiences.Length == 1 ? audiences[0]
            : null;
        if (clientId is null) return;

        // ValidateClientPostLogoutRedirectUri throws for an unknown client_id: it relies on ValidateClientId,
        // which ran before this handler. A client that has since been disabled or deleted is left
        // uninferred, so the URI gets OpenIddict's 400.
        var cancellationToken = context.Transaction.GetHttpRequest()?.HttpContext.RequestAborted ?? CancellationToken.None;
        if (await applications.FindByClientIdAsync(clientId, cancellationToken) is null) return;

        context.Request.ClientId = clientId;
    }
}
