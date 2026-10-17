using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using WardenId.SharedKernel.Application.Abstractions;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// Right after OpenIddict stores the access-token row of a client_credentials grant, records its tenant and public
/// client_id in the row's Properties (<see cref="AuthorizationOwnerProperties"/>), as <see cref="StampAuthorizationOwner"/>
/// does for authorizations. These tokens have no authorization, and the row's subject (the client_id) is unique per
/// tenant only, so without the stamp revoking a client's tokens (W12) couldn't tell tenants apart. Failures propagate
/// (→ 500): an unstamped token couldn't be revoked by client.
/// </summary>
public sealed class StampClientCredentialsTokenOwner(IOpenIddictTokenManager tokens, ITenantContext tenantContext)
    : IOpenIddictServerHandler<ProcessSignInContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessSignInContext>()
            .UseScopedHandler<StampClientCredentialsTokenOwner>()
            // GenerateAccessToken creates the row and puts its id on AccessTokenPrincipal.
            .SetOrder(OpenIddictServerHandlers.GenerateAccessToken.Descriptor.Order + 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ProcessSignInContext context)
    {
        if (context.IsRejected
            || context.EndpointType is not OpenIddictServerEndpointType.Token
            || !context.Request.IsClientCredentialsGrantType()
            || context.AccessTokenPrincipal?.GetTokenId() is not { Length: > 0 } tokenId
            || context.Request.ClientId is not { Length: > 0 } clientId
            || tenantContext.TenantId is not { } tenantId)
        {
            return;
        }

        var cancellationToken = context.Transaction.GetHttpRequest()?.HttpContext.RequestAborted ?? CancellationToken.None;

        if (await tokens.FindByIdAsync(tokenId, cancellationToken) is not { } token)
            return;

        var descriptor = new OpenIddictTokenDescriptor();
        await tokens.PopulateAsync(descriptor, token, cancellationToken);
        AuthorizationOwnerProperties.Write(descriptor.Properties, tenantId, clientId);
        await tokens.UpdateAsync(token, descriptor, cancellationToken);
    }
}
