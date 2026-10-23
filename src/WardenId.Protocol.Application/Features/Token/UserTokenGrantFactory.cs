using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.Protocol.Application.Features.Policies;
using WardenId.Protocol.Application.Utilities;
using WardenId.Protocol.Domain;
using GetForAuthenticationQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication.Query;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;

namespace WardenId.Protocol.Application.Features.Token;

/// <summary>
/// Shared by code and refresh redemption: re-checks the user (may have been disabled since sign-in) and
/// the client, sources claims fresh from Identity filtered by claimScopes, and computes
/// lifetimes (access and ID token: client override ?? tenant override ?? global default). The refresh lifetime is
/// decided by grantedScopes (offline_access) and the client still holding the refresh grant; it slides (client
/// override ?? tenant ?? global RefreshTokenLifetime) but never past auth_time + the tenant's (or global)
/// RefreshTokenAbsoluteLifetime.
/// </summary>
public sealed class UserTokenGrantFactory(
    ISender sender, IOptions<ProtocolTokenOptions> options, ITenantLifetimes tenantLifetimes, TimeProvider timeProvider)
{
    public async Task<Result<UserTokenGrant>> CreateAsync(
        Guid tenantId, string slug, string clientId, Guid userId,
        IReadOnlyCollection<string> grantedScopes, IReadOnlyCollection<string> claimScopes,
        DateTimeOffset authTime, CancellationToken cancellationToken)
    {
        var userResult = await sender.Send(new GetForSignInQuery(tenantId, userId), cancellationToken);
        if (userResult.IsFailed || !SignInUserRules.IsUsable(userResult.Value))
            return Result.Fail<UserTokenGrant>(ProtocolErrors.InvalidGrant());

        var clientResult = await sender.Send(new GetForAuthenticationQuery(tenantId, clientId), cancellationToken);
        if (clientResult.IsFailed || !clientResult.Value.Enabled)
            return Result.Fail<UserTokenGrant>(ProtocolErrors.InvalidGrant());

        var user = userResult.Value;
        var client = clientResult.Value;
        var settings = options.Value;
        var lifetimes = await tenantLifetimes.GetAsync(tenantId, cancellationToken);

        // The refresh grant may have been removed since /authorize: no refresh lifetime then, and the
        // principal drops offline_access so OpenIddict issues no refresh token (UserTokenGrantExtensions).
        TimeSpan? refreshLifetime = null;
        if (grantedScopes.Contains(OidcScopes.OfflineAccess) && client.AllowedGrants.Contains(AllowedGrant.RefreshToken))
        {
            var sliding = lifetimes.RefreshTokenFor(client.RefreshTokenLifetimeSeconds);
            var remaining = authTime + lifetimes.RefreshTokenAbsolute - timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero) return Result.Fail<UserTokenGrant>(ProtocolErrors.SessionExpired());
            refreshLifetime = remaining < sliding ? remaining : sliding;
        }

        var claims = ScopedUserClaims.From(user, claimScopes);

        return Result.Ok(new UserTokenGrant(
            Subject: user.UserId.ToString(),
            TenantId: tenantId,
            Audience: settings.AudienceFor(slug),
            AccessTokenLifetime: lifetimes.AccessTokenFor(client.AccessTokenLifetimeSeconds),
            IdTokenLifetime: lifetimes.IdTokenFor(client.IdTokenLifetimeSeconds),
            RefreshTokenLifetime: refreshLifetime,
            PreferredUsername: claims.PreferredUsername,
            GivenName: claims.GivenName,
            FamilyName: claims.FamilyName,
            Name: claims.Name,
            Email: claims.Email,
            EmailVerified: claims.EmailVerified));
    }
}
