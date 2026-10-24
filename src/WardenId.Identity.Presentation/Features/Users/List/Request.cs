using WardenId.Identity.Query.Contracts.Features.Users;
using WardenId.Identity.Query.Contracts.Features.Users.List;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Identity.Presentation.Features.Users.List;

// The sibling namespace WardenId.Identity.Query would otherwise shadow the imported type.
using Query = WardenId.Identity.Application.Features.Users.List.Query;

public sealed record Request(
    int PageNumber = 1,
    int PageSize = 10,
    SortDirection SortDirection = SortDirection.Desc,
    UserStatus? Status = null,
    string? Search = null,
    SortOption SortOption = SortOption.CreatedAt) : PaginationListRequest(PageNumber, PageSize, SortDirection)
{
    public Query ToQuery(Guid tenantId) =>
        new(tenantId, PageNumber, PageSize, SortDirection)
        {
            Status = Status,
            Search = Search,
            SortOption = SortOption
        };
}
