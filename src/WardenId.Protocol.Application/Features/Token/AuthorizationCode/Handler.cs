using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Domain;
using GetForAuthenticationQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication.Query;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;

namespace WardenId.Protocol.Application.Features.Token.AuthorizationCode;

/// <summary>
/// Re-checks the user at redemption (they may have been disabled since /authorize) and sources
/// ID-token claims fresh from Identity, filtered by the scopes the code carries.
/// </summary>
public sealed class Handler(ISender sender, IOptions<ProtocolTokenOptions> options)
    : IRequestHandler<Command, Result<AuthorizationCodeGrant>>
{
    public async Task<Result<AuthorizationCodeGrant>> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await sender.Send(new GetForSignInQuery(request.TenantId, request.UserId), cancellationToken);
        if (userResult.IsFailed) return Result.Fail<AuthorizationCodeGrant>(ProtocolErrors.InvalidGrant());

        var user = userResult.Value;
        if (!SignInUserRules.IsUsable(user))
            return Result.Fail<AuthorizationCodeGrant>(ProtocolErrors.InvalidGrant());

        var clientResult = await sender.Send(new GetForAuthenticationQuery(request.TenantId, request.ClientId), cancellationToken);
        if (clientResult.IsFailed) return Result.Fail<AuthorizationCodeGrant>(ProtocolErrors.InvalidGrant());

        var settings = options.Value;
        var accessLifetime = clientResult.Value.AccessTokenLifetimeSeconds is { } seconds
            ? TimeSpan.FromSeconds(seconds)
            : settings.AccessTokenLifetime;

        var profile = request.Scopes.Contains(OidcScopes.Profile);
        var email = request.Scopes.Contains(OidcScopes.Email);

        return Result.Ok(new AuthorizationCodeGrant(
            Subject: user.UserId.ToString(),
            TenantId: request.TenantId,
            Audience: settings.AudienceFor(request.Slug),
            AccessTokenLifetime: accessLifetime,
            IdTokenLifetime: settings.IdTokenLifetime,
            PreferredUsername: profile ? user.Username : null,
            Email: email ? user.Email : null,
            // Active with no pending actions ⇒ the address was verified (VerifyEmail is what activates).
            EmailVerified: email ? true : null));
    }
}
