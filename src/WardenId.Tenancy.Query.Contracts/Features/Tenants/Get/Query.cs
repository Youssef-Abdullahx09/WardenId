namespace WardenId.Tenancy.Query.Contracts.Features.Tenants.Get;

/// <summary>A tenant by id, with the public metadata of all its signing keys.</summary>
public sealed record Query(Guid TenantId);
