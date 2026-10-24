using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Protocol.Query.Contracts.Features.Sessions.List;

/// <summary>
/// One page of a tenant's sessions. Without <see cref="IncludeEnded"/> only live sessions (not ended,
/// <c>ExpiresAt</c> after <see cref="Now"/>); with it, ended and expired ones too.
/// </summary>
public sealed record Query(
    Guid TenantId,
    int PageNumber,
    int PageSize,
    SortDirection SortDirection) : PaginationListRequest(PageNumber, PageSize, SortDirection)
{
    /// <summary>Only this user's sessions; null for every user.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Only sessions this client (public client_id) took part in; null or blank for any.</summary>
    public string? ClientId { get; init; }

    public bool IncludeEnded { get; init; }

    /// <summary>The instant a session's <c>ExpiresAt</c> is compared to when deciding whether it's live.</summary>
    public required DateTimeOffset Now { get; init; }

    public SortOption SortOption { get; init; } = SortOption.AuthTime;
}

/// <summary>The column a session list page is ordered by; the session id breaks ties in the same direction.</summary>
public enum SortOption
{
    AuthTime = 1,
    ExpiresAt = 2
}
