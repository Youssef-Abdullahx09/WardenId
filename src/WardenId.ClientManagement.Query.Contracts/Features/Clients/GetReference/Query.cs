namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.GetReference;

/// <summary>One non-deleted client of the tenant, by its Guid <paramref name="Id"/>; a disabled client is found.</summary>
public sealed record Query(
    Guid TenantId,
    Guid Id);
