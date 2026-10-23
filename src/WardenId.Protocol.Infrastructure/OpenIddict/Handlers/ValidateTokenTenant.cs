using OpenIddict.Abstractions;
using OpenIddict.Server;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.Application.Utilities;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// Every access/refresh token carries tenant_id; at introspection, revocation and userinfo it must equal
/// the path tenant. Needed because refresh tokens are Data Protection tokens (tenant keys never apply):
/// without it, an acme refresh token presented at globex by a client with the same client_id passes
/// OpenIddict's presenter check. The token endpoint checks tenant itself (grant-specific messages).
/// </summary>
public sealed class ValidateTokenTenant(ITenantContext tenantContext) : IOpenIddictServerHandler<ValidateTokenContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenContext>()
            .UseScopedHandler<ValidateTokenTenant>()
            .SetOrder(OpenIddictServerHandlers.Protection.ValidatePrincipal.Descriptor.Order + 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(ValidateTokenContext context)
    {
        if (context.EndpointType is OpenIddictServerEndpointType.Token || context.Principal is not { } principal)
            return default;

        // Identity tokens carry no tenant_id; they are bound by the tenant signing key (AttachTenantValidationKeys).
        if (!principal.HasTokenType(TokenTypeIdentifiers.AccessToken) && !principal.HasTokenType(TokenTypeIdentifiers.RefreshToken))
            return default;

        if (tenantContext.TenantId is not { } tenantId || principal.GetClaim(CustomClaimTypes.TenantId) != tenantId.ToString())
            context.Reject(error: Errors.InvalidToken, description: "The token is not valid for this tenant.");

        return default;
    }
}
