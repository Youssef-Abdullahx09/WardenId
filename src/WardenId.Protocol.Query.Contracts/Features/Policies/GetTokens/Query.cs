namespace WardenId.Protocol.Query.Contracts.Features.Policies.GetTokens;

/// <summary>A tenant's stored token and session lifetime overrides, uncached.</summary>
public sealed record Query(Guid TenantId);
