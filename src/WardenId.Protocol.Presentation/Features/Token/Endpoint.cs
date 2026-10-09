using MediatR;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using WardenId.Protocol.Application.Features.Token.ClientCredentials;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.Presentation;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Presentation.Features.Token;

/// <summary>
/// OpenIddict passthrough target: by the time this runs OpenIddict has validated the request,
/// the tenant and the client. This only decides the token contents and signs in.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(ProtocolRoutes.TenantIssuer + "/token", async (
                HttpContext httpContext,
                [FromServices] ISender sender,
                [FromServices] ITenantContext tenantContext,
                CancellationToken cancellationToken) =>
            {
                // Null when OpenIddict didn't handle the request (e.g. mixed-case slug: the route
                // regex is case-insensitive, the issuer is not) -> not a token endpoint.
                var request = httpContext.GetOpenIddictServerRequest();
                if (request is null
                    || tenantContext.TenantId is not { } tenantId
                    || tenantContext.Slug is not { } slug)
                {
                    return Results.NotFound();
                }

                if (!request.IsClientCredentialsGrantType())
                    return Forbid(Errors.UnsupportedGrantType, "The grant type is not supported.");

                var result = await sender.Send(new Command(tenantId, slug, request.ClientId!), cancellationToken);
                if (result.IsFailed)
                    return Forbid(Errors.InvalidClient, "The client is not valid.");

                return Results.SignIn(
                    result.Value.ToPrincipal(),
                    authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            })
            .AllowAnonymous()
            .WithTags("Protocol - Token")
            .WithSummary("OAuth 2.0 token endpoint (client_credentials)")
            .ExcludeFromDescription();

    private static IResult Forbid(string error, string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
