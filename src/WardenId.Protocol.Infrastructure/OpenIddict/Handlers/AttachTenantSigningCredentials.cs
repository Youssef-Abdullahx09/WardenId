using MediatR;
using Microsoft.AspNetCore;
using OpenIddict.Server;
using WardenId.Protocol.Application.Abstractions;
using WardenId.SharedKernel.Application.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using GetActiveForSigningQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.Query;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// After the built-in AttachSecurityCredentials: access and identity tokens are signed with the
/// tenant's active ES256 key, fetched per request. OpenIddict's own server keys are placeholders
/// and must never sign a token a relying party sees.
/// </summary>
public sealed class AttachTenantSigningCredentials(
    ISender sender, ITenantSigningCredentialsFactory credentialsFactory, ITenantContext tenantContext)
    : IOpenIddictServerHandler<GenerateTokenContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<GenerateTokenContext>()
            .UseScopedHandler<AttachTenantSigningCredentials>()
            .SetOrder(OpenIddictServerHandlers.Protection.AttachSecurityCredentials.Descriptor.Order + 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(GenerateTokenContext context)
    {
        if (context.TokenType is not (TokenTypeIdentifiers.AccessToken or TokenTypeIdentifiers.IdentityToken)) return;

        var httpContext = context.Transaction.GetHttpRequest()?.HttpContext
            ?? throw new InvalidOperationException("The ASP.NET Core request cannot be resolved.");

        if (tenantContext.TenantId is not { } tenantId)
            throw new InvalidOperationException("Token generation requires a resolved tenant.");

        var keyResult = await sender.Send(new GetActiveForSigningQuery(tenantId), httpContext.RequestAborted);
        if (keyResult.IsFailed)
            throw new InvalidOperationException($"Tenant '{tenantId}' has no active signing key.");

        var key = keyResult.Value;
        context.SigningCredentials = credentialsFactory.Create(
            new ActiveSigningKey(key.KeyId, key.Algorithm, key.EncryptedPrivateKey, key.Slug));
    }
}
