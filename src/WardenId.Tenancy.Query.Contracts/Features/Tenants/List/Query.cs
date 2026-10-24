using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Tenancy.Query.Contracts.Features.Tenants.List;

/// <summary>One page of tenants.</summary>
public sealed record Query(
    int PageNumber,
    int PageSize,
    SortDirection SortDirection) : PaginationListRequest(PageNumber, PageSize, SortDirection)
{
    /// <summary>Only tenants in this status; null for any.</summary>
    public Status? Status { get; init; }

    public SortOption SortOption { get; init; }
}

/// <summary>The column a tenant list page is ordered by; the tenant id breaks ties in the same direction.</summary>
public enum SortOption
{
    CreatedAt = 1,
    Slug = 2,
    DisplayName = 3
}
