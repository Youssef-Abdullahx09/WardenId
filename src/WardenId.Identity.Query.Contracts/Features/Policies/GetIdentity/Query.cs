namespace WardenId.Identity.Query.Contracts.Features.Policies.GetIdentity;

/// <summary>The tenant's stored identity policy overrides.</summary>
public sealed record Query(Guid TenantId);
