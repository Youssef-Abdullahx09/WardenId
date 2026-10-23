using MediatR;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using WardenId.Protocol.Application.Features.UserInfo;
using WardenId.Protocol.Application.Utilities;
using WardenId.Protocol.Presentation.Utilities;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.Application.Utilities;
using WardenId.SharedKernel.Presentation;
using static OpenIddict.Abstractions.OpenIddictConstants;
using UserInfoQuery = WardenId.Protocol.Application.Features.UserInfo.Query;

namespace WardenId.Protocol.Presentation.Features.UserInfo;

/// <summary>
/// OpenIddict passthrough target: OpenIddict has already validated the access token (tenant keys,
/// tenant binding, token/authorization status). Claims come fresh from Identity, filtered by the
/// token's scopes; an unusable user gets the same invalid_token as a bad token (no state disclosure).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    private const string InvalidToken = "The access token is not valid.";

    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapMethods(ProtocolRoutes.UnderTenantIssuer(ProtocolPaths.Endpoints.UserInfo), [HttpMethods.Get, HttpMethods.Post], async (
                HttpContext httpContext,
                [FromServices] ISender sender,
                [FromServices] ITenantContext tenantContext,
                CancellationToken cancellationToken) =>
            {
                // Null when OpenIddict didn't handle the request (e.g. mixed-case slug).
                if (httpContext.GetOpenIddictServerRequest() is null || tenantContext.TenantId is not { } tenantId)
                    return Results.NotFound();

                var authentication = await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                if (authentication.Principal is not { } principal
                    || principal.GetClaim(CustomClaimTypes.TenantId) != tenantId.ToString())
                {
                    return OpenIddictResults.Challenge(Errors.InvalidToken, InvalidToken);
                }

                // Before the subject check: client_credentials tokens carry no user subject.
                if (!principal.HasScope(Scopes.OpenId))
                    return OpenIddictResults.Challenge(Errors.InsufficientScope, "The access token lacks the 'openid' scope.");

                if (!Guid.TryParse(principal.GetClaim(Claims.Subject), out var userId))
                    return OpenIddictResults.Challenge(Errors.InvalidToken, InvalidToken);

                var result = await sender.Send(new UserInfoQuery(tenantId, userId, principal.GetScopes()), cancellationToken);
                
                return result.IsFailed ? 
                    OpenIddictResults.Challenge(Errors.InvalidToken, InvalidToken) 
                    : Results.Ok(ToResponse(result.Value));
            })
            .AllowAnonymous()
            .WithTags(EndpointTags.UserInfo)
            .WithSummary("OIDC UserInfo endpoint")
            .ExcludeFromDescription();

    // Only claims the scopes released; absent claims are omitted, not null.
    private static Dictionary<string, object> ToResponse(UserInfoClaims userInfo)
    {
        var response = new Dictionary<string, object> { [Claims.Subject] = userInfo.Subject };
        if (userInfo.Claims.PreferredUsername is { } username) response[Claims.PreferredUsername] = username;
        if (userInfo.Claims.Email is { } email) response[Claims.Email] = email;
        if (userInfo.Claims.EmailVerified is { } verified) response[Claims.EmailVerified] = verified;
        return response;
    }
}
