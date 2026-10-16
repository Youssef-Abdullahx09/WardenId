namespace WardenId.Protocol.Query.Contracts.Features.Sessions.List;

/// <summary>
/// One page of a tenant's sessions, newest <c>AuthTime</c> first. Without <paramref name="IncludeEnded"/> only live
/// sessions (not ended, <c>ExpiresAt</c> after <paramref name="Now"/>); with it, ended and expired ones too.
/// </summary>
/// <param name="UserId">Only this user's sessions; null for every user.</param>
/// <param name="ClientId">Only sessions this client (public client_id) took part in; null for any.</param>
public sealed record Query(
    Guid TenantId,
    Guid? UserId,
    string? ClientId,
    bool IncludeEnded,
    DateTimeOffset Now,
    int PageNumber,
    int PageSize);
