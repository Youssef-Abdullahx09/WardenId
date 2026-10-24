using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Identity.Query.Contracts.Features.Users.List;

/// <summary>
/// One page of a tenant's users. Soft-deleted users are left out unless <see cref="IncludeDeleted"/>.
/// </summary>
public sealed record Query(
    Guid TenantId,
    int PageNumber,
    int PageSize,
    SortDirection SortDirection) : PaginationListRequest(PageNumber, PageSize, SortDirection)
{
    public bool IncludeDeleted { get; init; }

    /// <summary>Only users in this status; null for any.</summary>
    public UserStatus? Status { get; init; }

    /// <summary>
    /// Case-insensitive prefix of the email, username, first name, last name or "first last", trimmed and matched
    /// literally (% and _ are not wildcards); null or blank for no filter.
    /// </summary>
    public string? Search { get; init; }

    public SortOption SortOption { get; init; } = SortOption.CreatedAt;
}

/// <summary>The column a user list page is ordered by; the user id breaks ties in the same direction.</summary>
public enum SortOption
{
    CreatedAt = 1,
    Email = 2,
    Username = 3,
    FirstName = 4,
    LastName = 5
}
