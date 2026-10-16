using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using WardenId.SharedKernel.Application.Abstractions;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// Right after OpenIddict creates the ad-hoc authorization for an /authorize sign-in, records its tenant and public
/// client_id in the row's Properties (<see cref="AuthorizationOwnerProperties"/>). OpenIddict stores neither, and the
/// admin grant endpoints need both: the tenant proves a row belongs to the tenant in the route, the client names it.
/// Refresh and code redemptions reuse the authorization, so only authorization-endpoint sign-ins are stamped. Failures
/// propagate (→ 500): an unstamped grant would be invisible to the admin API.
/// </summary>
public sealed class StampAuthorizationOwner(IOpenIddictAuthorizationManager authorizations, ITenantContext tenantContext)
    : IOpenIddictServerHandler<ProcessSignInContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessSignInContext>()
            .UseScopedHandler<StampAuthorizationOwner>()
            // After RecordSessionAuthorization (+1): a sign-in it rejects (session ended) is not stamped.
            .SetOrder(OpenIddictServerHandlers.AttachAuthorization.Descriptor.Order + 2)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ProcessSignInContext context)
    {
        if (context.IsRejected
            || context.EndpointType is not OpenIddictServerEndpointType.Authorization
            || context.Principal?.GetAuthorizationId() is not { Length: > 0 } authorizationId
            || context.Request.ClientId is not { Length: > 0 } clientId
            || tenantContext.TenantId is not { } tenantId)
        {
            return;
        }

        var cancellationToken = context.Transaction.GetHttpRequest()?.HttpContext.RequestAborted ?? CancellationToken.None;

        if (await authorizations.FindByIdAsync(authorizationId, cancellationToken) is not { } authorization)
            return;

        var descriptor = new OpenIddictAuthorizationDescriptor();
        await authorizations.PopulateAsync(descriptor, authorization, cancellationToken);
        AuthorizationOwnerProperties.Write(descriptor.Properties, tenantId, clientId);
        await authorizations.UpdateAsync(authorization, descriptor, cancellationToken);
    }
}
