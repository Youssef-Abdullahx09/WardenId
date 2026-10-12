using MediatR;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using WardenId.SharedKernel.Application;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.Presentation;
using AuthorizationCodeCommand = WardenId.Protocol.Application.Features.Token.AuthorizationCode.Command;
using ClientCredentialsCommand = WardenId.Protocol.Application.Features.Token.ClientCredentials.Command;
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

                if (request.IsAuthorizationCodeGrantType())
                {
                    // OpenIddict already verified the code (single use), PKCE verifier, redirect_uri and
                    // client binding. The tenant check stops a code from one tenant being redeemed at
                    // another where a client with the same client_id exists.
                    var code = await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                    if (code.Principal is not { } codePrincipal
                        || codePrincipal.GetClaim(CustomClaimTypes.TenantId) != tenantId.ToString()
                        || !Guid.TryParse(codePrincipal.GetClaim(Claims.Subject), out var userId))
                    {
                        return OpenIddictResults.Forbid(Errors.InvalidGrant, "The authorization code is not valid.");
                    }

                    var grant = await sender.Send(
                        new AuthorizationCodeCommand(tenantId, slug, request.ClientId!, userId, codePrincipal.GetScopes()),
                        cancellationToken);
                    if (grant.IsFailed)
                        return OpenIddictResults.Forbid(Errors.InvalidGrant, "The user can no longer sign in.");

                    return Results.SignIn(
                        grant.Value.ToPrincipal(codePrincipal),
                        authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                }

                if (!request.IsClientCredentialsGrantType())
                    return OpenIddictResults.Forbid(Errors.UnsupportedGrantType, "The grant type is not supported.");

                var result = await sender.Send(new ClientCredentialsCommand(tenantId, slug, request.ClientId!), cancellationToken);
                if (result.IsFailed)
                    return OpenIddictResults.Forbid(Errors.InvalidClient, "The client is not valid.");

                return Results.SignIn(
                    result.Value.ToPrincipal(),
                    authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            })
            .AllowAnonymous()
            .WithTags("Protocol - Token")
            .WithSummary("OAuth 2.0 token endpoint (authorization_code, client_credentials)")
            .ExcludeFromDescription();
}
