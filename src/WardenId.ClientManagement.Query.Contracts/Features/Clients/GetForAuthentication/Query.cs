namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.GetForAuthentication;

/// <summary>One non-deleted client of the tenant, by its public <paramref name="ClientId"/>; a disabled client is found.</summary>
public sealed record Query(
    Guid TenantId,
    string ClientId);
