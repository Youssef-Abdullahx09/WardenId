namespace WardenId.Protocol.Application.Features.Grants;

/// <summary>
/// Revokes what OpenIddict stored for one client of one tenant: every valid authorization stamped with that tenant and
/// client_id (with all its tokens), and the client_credentials access-token rows stamped the same way. Rows are found
/// only by the owner stamp, since OpenIddict's own columns name neither tenant nor client (ADR-01), so rows written
/// before stamping are not found. Idempotent.
/// <para>
/// Access tokens are JWTs: the Gateway's API authentication doesn't consult these rows, so an issued access token
/// stays usable there until it expires. Revocation is visible to the refresh grant and to introspection.
/// </para>
/// </summary>
public interface IClientGrantRevoker
{
    /// <param name="clientId">The public client_id (unique per tenant only).</param>
    Task RevokeAsync(Guid tenantId, string clientId, CancellationToken cancellationToken);
}
