namespace WardenId.Identity.Query.Contracts.Features.Users.GetForSignIn;

/// <summary>One live (not soft-deleted) user of the tenant, as sign-in and token issuance need it.</summary>
public sealed record Query(Guid TenantId, Guid UserId);
