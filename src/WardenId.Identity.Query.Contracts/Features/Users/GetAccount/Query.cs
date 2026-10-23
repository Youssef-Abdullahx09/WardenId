namespace WardenId.Identity.Query.Contracts.Features.Users.GetAccount;

/// <summary>One live (not soft-deleted) user of the tenant, as the user's own account page shows it.</summary>
public sealed record Query(Guid TenantId, Guid UserId);
