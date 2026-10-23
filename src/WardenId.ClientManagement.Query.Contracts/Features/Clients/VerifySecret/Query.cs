namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.VerifySecret;

/// <summary>
/// What verifying a secret needs of one non-deleted client of the tenant, by its public <paramref name="ClientId"/>:
/// its state and every secret hash, expired ones included. Eligibility and matching stay with the caller.
/// </summary>
public sealed record Query(
    Guid TenantId,
    string ClientId);
