using MediatR;
using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using WardenId.Protocol.Application;
using WardenId.SharedKernel.Application.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using RecordAuthorizationCommand = WardenId.Protocol.Application.Features.Sessions.RecordAuthorization.Command;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// Right after OpenIddict creates the ad-hoc authorization for an /authorize sign-in, links it to the
/// browser session (<c>sid</c>). That authorization id is what session end revokes (unless the grant has
/// offline_access) and how back-channel logout finds the participating clients. Refresh redemptions
/// reuse the existing authorization id, so only authorization-endpoint sign-ins are recorded. A session
/// that ended between the cookie check and here fails the sign-in with login_required.
/// </summary>
public sealed class RecordSessionAuthorization(ISender sender, ITenantContext tenantContext)
    : IOpenIddictServerHandler<ProcessSignInContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessSignInContext>()
            .UseScopedHandler<RecordSessionAuthorization>()
            .SetOrder(OpenIddictServerHandlers.AttachAuthorization.Descriptor.Order + 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ProcessSignInContext context)
    {
        if (context.IsRejected
            || context.EndpointType is not OpenIddictServerEndpointType.Authorization
            || context.Principal is not { } principal
            || !Guid.TryParse(principal.GetClaim(SessionClaimTypes.SessionId), out var sid)
            || principal.GetAuthorizationId() is not { Length: > 0 } authorizationId
            || context.Request.ClientId is not { Length: > 0 } clientId
            || tenantContext.TenantId is not { } tenantId)
        {
            return;
        }

        var cancellationToken = context.Transaction.GetHttpRequest()?.HttpContext.RequestAborted ?? CancellationToken.None;

        var result = await sender.Send(
            new RecordAuthorizationCommand(tenantId, sid, authorizationId, clientId, principal.HasScope(Scopes.OfflineAccess)),
            cancellationToken);
        if (result.IsFailed)
            context.Reject(error: Errors.LoginRequired, description: "The session has ended.");
    }
}
