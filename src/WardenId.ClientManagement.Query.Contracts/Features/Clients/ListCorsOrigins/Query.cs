namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.ListCorsOrigins;

/// <summary>The distinct CORS origins of every enabled, non-deleted client of the tenant.</summary>
public sealed record Query(
    Guid TenantId);
