using OpenIddict.Abstractions;
using WardenId.Protocol.Application.Features.BackchannelLogout;

namespace WardenId.Protocol.Infrastructure.Sessions;

/// <summary>
/// Revokes each authorization and every token under it, the same family mechanism as RevokeTokenFamily, so a
/// rotated refresh token dies with its grant. Unknown (pruned) or already-revoked ids are no-ops, which makes a
/// CAP retry of the whole message safe.
/// </summary>
public sealed class OpenIddictSessionAuthorizationRevoker(
    IOpenIddictAuthorizationManager authorizations, IOpenIddictTokenManager tokens) : ISessionAuthorizationRevoker
{
    public async Task RevokeAsync(IReadOnlyCollection<string> authorizationIds, CancellationToken cancellationToken)
    {
        foreach (var authorizationId in authorizationIds)
        {
            if (await authorizations.FindByIdAsync(authorizationId, cancellationToken) is { } authorization)
                await authorizations.TryRevokeAsync(authorization, cancellationToken);

            await tokens.RevokeByAuthorizationIdAsync(authorizationId, cancellationToken);
        }
    }
}
