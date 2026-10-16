namespace WardenId.Protocol.Application.Features.Grants;

/// <summary>
/// Read access to OpenIddict's authorizations (one per /authorize sign-in; refresh tokens and the codes and access
/// tokens of that sign-in hang off it). Revocation goes through <see cref="BackchannelLogout.ISessionAuthorizationRevoker"/>,
/// which revokes the authorization and its whole token family.
/// </summary>
public interface IUserGrantStore
{
    /// <summary>Every authorization of <paramref name="subject"/>, any status and tenant. Callers filter by <see cref="StoredGrant.Owner"/>.</summary>
    Task<IReadOnlyList<StoredGrant>> ListBySubjectAsync(string subject, CancellationToken cancellationToken);

    Task<StoredGrant?> FindAsync(string authorizationId, CancellationToken cancellationToken);
}

/// <param name="Owner">Tenant and client stamped on the row at creation; null for rows created before stamping.</param>
public sealed record StoredGrant(
    string AuthorizationId,
    string? Subject,
    GrantOwner? Owner,
    DateTimeOffset? CreatedAt,
    IReadOnlyList<string> Scopes,
    GrantStatus Status)
{
    /// <summary>
    /// The row's subject is <paramref name="userId"/> and its tenant stamp is <paramref name="tenantId"/>. Token tables
    /// are not tenant-partitioned (ADR-01), so the stamp is the only proof a row belongs to the tenant in the route.
    /// </summary>
    public bool BelongsTo(Guid tenantId, Guid userId) =>
        Owner?.TenantId == tenantId && Subject == userId.ToString();
}

/// <param name="ClientId">The client's public client_id (unique per tenant only).</param>
public sealed record GrantOwner(Guid TenantId, string ClientId);
