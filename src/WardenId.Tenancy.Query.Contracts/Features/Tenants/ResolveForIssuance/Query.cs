namespace WardenId.Tenancy.Query.Contracts.Features.Tenants.ResolveForIssuance;

/// <summary>A tenant by slug (compared exactly), whatever its status; eligibility is the caller's decision.</summary>
public sealed record Query(string Slug);
