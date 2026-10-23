namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.Get;

/// <summary>One non-deleted client of the tenant, by its Guid <paramref name="Id"/>, with its secrets and signing keys.</summary>
public sealed record Query(
    Guid TenantId,
    Guid Id);
