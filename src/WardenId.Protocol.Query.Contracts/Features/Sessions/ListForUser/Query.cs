namespace WardenId.Protocol.Query.Contracts.Features.Sessions.ListForUser;

/// <summary>
/// Sessions of a user in a tenant, newest first. Without <paramref name="IncludeEnded"/> only live ones: not ended
/// and <c>ExpiresAt</c> after <paramref name="Now"/>; with it, ended and expired ones too.
/// </summary>
public sealed record Query(
    Guid TenantId,
    Guid UserId,
    DateTimeOffset Now,
    bool IncludeEnded = false);
