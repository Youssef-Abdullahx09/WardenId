namespace WardenId.Identity.Query.Contracts.Features.Users.Get;

/// <summary>One live (not soft-deleted) user of the tenant, with its attributes and MFA factors.</summary>
public sealed record Query(Guid TenantId, Guid UserId);
