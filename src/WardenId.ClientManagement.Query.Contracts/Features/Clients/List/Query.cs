using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.List;

/// <summary>One page of a tenant's non-deleted clients.</summary>
public sealed record Query(
    Guid TenantId,
    int PageNumber,
    int PageSize,
    SortDirection SortDirection) : PaginationListRequest(PageNumber, PageSize, SortDirection)
{
    /// <summary>Only clients in this state; null for any.</summary>
    public bool? Enabled { get; init; }

    /// <summary>Only clients of this type; null for any.</summary>
    public ClientType? ClientType { get; init; }

    public SortOption SortOption { get; init; }
}

/// <summary>The column a client list page is ordered by; the client's row id breaks ties in the same direction.</summary>
public enum SortOption
{
    CreatedAt = 1,
    ClientId = 2,
    DisplayName = 3
}
