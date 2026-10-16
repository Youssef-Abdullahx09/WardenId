using System.Globalization;
using System.Security.Claims;
using OpenIddict.Abstractions;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// What a token principal tells the audit trail: the client, the user, the grant's token family and the session.
/// Only identifiers — never a token value (audit spec I9).
/// </summary>
internal static class TokenAuditFacts
{
    public static List<Target> Targets(string clientId, ClaimsPrincipal? principal)
    {
        List<Target> targets = [Target.Client(clientId)];
        if (Guid.TryParse(principal?.GetClaim(Claims.Subject), out var userId))
            targets.Add(Target.User(userId));
        if (principal?.GetAuthorizationId() is { Length: > 0 } authorizationId)
            targets.Add(Target.TokenFamily(authorizationId));
        return targets;
    }

    public static SessionContext Session(ClaimsPrincipal? principal, string? tokenId)
    {
        Guid? sid = Guid.TryParse(principal?.GetClaim(SessionClaimTypes.SessionId), out var parsedSid) ? parsedSid : null;
        DateTimeOffset? authTime = long.TryParse(
            principal?.GetClaim(Claims.AuthenticationTime), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
        var amr = principal?.GetClaims(Claims.AuthenticationMethodReference) ?? [];
        return new SessionContext(sid, authTime, amr.Length > 0 ? amr.ToArray() : null, tokenId);
    }
}
