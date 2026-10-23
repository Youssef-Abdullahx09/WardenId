namespace WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetPublicKeys;

/// <summary>A tenant by slug (compared exactly), whatever its status, with every key's status and public JWK.</summary>
public sealed record Query(string Slug);
