namespace WardenId.Protocol.Application.Features.BackchannelLogout;

/// <summary>
/// Revokes OpenIddict authorizations and every token issued under them. Unknown or already-revoked ids
/// must be no-ops, so a retried message is safe.
/// </summary>
public interface ISessionAuthorizationRevoker
{
    Task RevokeAsync(IReadOnlyCollection<string> authorizationIds, CancellationToken cancellationToken);
}
