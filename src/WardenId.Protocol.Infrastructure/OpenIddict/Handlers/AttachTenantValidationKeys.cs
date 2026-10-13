using MediatR;
using Microsoft.AspNetCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using WardenId.SharedKernel.Application.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using GetPublicKeysQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys.Query;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// Validation twin of AttachTenantSigningCredentials: JWTs (access/identity tokens) are verified only
/// against the path tenant's public keys (the JWKS set, rotation overlap included). The issuer is
/// already per tenant (ResolveTokenValidationParameters uses BaseUri). Fails closed: without tenant
/// keys nothing validates — OpenIddict's placeholder server keys are never accepted for access/identity tokens.
/// </summary>
public sealed class AttachTenantValidationKeys(ISender sender, ITenantContext tenantContext)
    : IOpenIddictServerHandler<ValidateTokenContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenContext>()
            .UseScopedHandler<AttachTenantValidationKeys>()
            .SetOrder(OpenIddictServerHandlers.Protection.ResolveTokenValidationParameters.Descriptor.Order + 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ValidateTokenContext context)
    {
        // Data Protection tokens (codes, refresh tokens) never reach JWT validation; an empty set means "any type".
        if (context.ValidTokenTypes.Count > 0
            && !context.ValidTokenTypes.Contains(TokenTypeIdentifiers.AccessToken)
            && !context.ValidTokenTypes.Contains(TokenTypeIdentifiers.IdentityToken))
        {
            return;
        }

        var parameters = context.TokenValidationParameters;
        parameters.IssuerSigningKeyResolver = null;
        parameters.IssuerSigningKeys = [];

        if (tenantContext.Slug is not { } slug) return;

        var httpContext = context.Transaction.GetHttpRequest()?.HttpContext
            ?? throw new InvalidOperationException("The ASP.NET Core request cannot be resolved.");

        var result = await sender.Send(new GetPublicKeysQuery(slug), httpContext.RequestAborted);
        if (result.IsFailed) return;

        // An unparseable stored JWK is an invariant violation (as in /jwks): JsonWebKey throws → 500.
        parameters.IssuerSigningKeys = result.Value.PublicKeyJwks.Select(jwk => new JsonWebKey(jwk)).ToList();
    }
}
