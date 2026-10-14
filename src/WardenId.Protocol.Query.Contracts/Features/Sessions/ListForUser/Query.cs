namespace WardenId.Protocol.Query.Contracts.Features.Sessions.ListForUser;

/// <summary>Live sessions of a user in a tenant: not ended and <c>ExpiresAt</c> after <paramref name="Now"/>.</summary>
public sealed record Query(
    Guid TenantId,
    Guid UserId,
    DateTimeOffset Now);
