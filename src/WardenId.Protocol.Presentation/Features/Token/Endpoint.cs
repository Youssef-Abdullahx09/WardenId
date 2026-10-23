using System.Globalization;
using System.Security.Claims;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using WardenId.Protocol.Application.Utilities;
using WardenId.Protocol.Domain;
using WardenId.Protocol.Presentation.Utilities;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.Application.Utilities;
using WardenId.SharedKernel.Presentation;
using AuthorizationCodeCommand = WardenId.Protocol.Application.Features.Token.AuthorizationCode.Command;
using ClientCredentialsCommand = WardenId.Protocol.Application.Features.Token.ClientCredentials.Command;
using RefreshTokenCommand = WardenId.Protocol.Application.Features.Token.RefreshToken.Command;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Presentation.Features.Token;

/// <summary>
/// OpenIddict passthrough target: by the time this runs OpenIddict has validated the request,
/// the tenant and the client. This only decides the token contents and signs in.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    private static bool TryGetAuthTime(ClaimsPrincipal principal, out DateTimeOffset authTime)
    {
        authTime = default;
        if (!long.TryParse(principal.GetClaim(Claims.AuthenticationTime), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            return false;
        authTime = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return true;
    }

    // Same message for every user/client state (no enumeration); only the absolute cap is told apart.
    private static string Describe(IResultBase result) =>
        result.Errors.Count > 0
        && result.Errors[0].Metadata.TryGetValue(ProtocolErrorCodes.MetadataKey, out var code)
        && code as string == ProtocolErrorCodes.SessionExpired
            ? "The session has expired."
            : "The user can no longer sign in.";

    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(ProtocolRoutes.UnderTenantIssuer(ProtocolPaths.Endpoints.Token), async (
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
                        || !Guid.TryParse(codePrincipal.GetClaim(Claims.Subject), out var userId)
                        || !TryGetAuthTime(codePrincipal, out var authTime))
                    {
                        return OpenIddictResults.Forbid(Errors.InvalidGrant, "The authorization code is not valid.");
                    }

                    var grant = await sender.Send(
                        new AuthorizationCodeCommand(tenantId, slug, request.ClientId!, userId, codePrincipal.GetScopes(), authTime),
                        cancellationToken);
                    if (grant.IsFailed)
                        return OpenIddictResults.Forbid(Errors.InvalidGrant, Describe(grant));

                    return Results.SignIn(
                        grant.Value.ToPrincipal(codePrincipal),
                        authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                }

                if (request.IsRefreshTokenGrantType())
                {
                    // OpenIddict already verified the refresh token (format, expiry, presenter = this client,
                    // single use with leeway 0 — a replay revokes the whole authorization). The tenant check
                    // stops a token from one tenant being redeemed at another with the same client_id.
                    var refresh = await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                    if (refresh.Principal is not { } refreshPrincipal
                        || refreshPrincipal.GetClaim(CustomClaimTypes.TenantId) != tenantId.ToString()
                        || !Guid.TryParse(refreshPrincipal.GetClaim(Claims.Subject), out var refreshUserId)
                        || !TryGetAuthTime(refreshPrincipal, out var refreshAuthTime))
                    {
                        return OpenIddictResults.Forbid(Errors.InvalidGrant, "The refresh token is not valid.");
                    }

                    // RFC 6749 §6: an optional narrower scope. Scopes beyond the original grant are rejected
                    // (invalid_scope) before passthrough by RejectRefreshScopeBeyondGrant. The principal keeps the
                    // original scopes so the rotated refresh token is identical in scope; OpenIddict narrows the
                    // access token itself (PrepareAccessTokenPrincipal), and the requested scopes filter user claims.
                    var requestedScopes = request.GetScopes();

                    var refreshed = await sender.Send(
                        new RefreshTokenCommand(tenantId, slug, request.ClientId!, refreshUserId, refreshPrincipal.GetScopes(), requestedScopes, refreshAuthTime),
                        cancellationToken);
                    if (refreshed.IsFailed)
                        return OpenIddictResults.Forbid(Errors.InvalidGrant, Describe(refreshed));

                    return Results.SignIn(
                        refreshed.Value.ToPrincipal(refreshPrincipal),
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
            .WithTags(EndpointTags.Token)
            .WithSummary("OAuth 2.0 token endpoint (authorization_code, refresh_token, client_credentials)")
            .ExcludeFromDescription();
}
