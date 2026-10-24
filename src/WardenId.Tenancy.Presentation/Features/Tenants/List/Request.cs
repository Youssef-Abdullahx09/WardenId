using WardenId.SharedKernel.Query.Contracts;
using WardenId.Tenancy.Query.Contracts.Features.Tenants;
using WardenId.Tenancy.Query.Contracts.Features.Tenants.List;

namespace WardenId.Tenancy.Presentation.Features.Tenants.List;

// The sibling namespace WardenId.Tenancy.Query would otherwise shadow the imported type.
using Query = WardenId.Tenancy.Application.Features.Tenants.List.Query;

public sealed record Request(
    int PageNumber = 1,
    int PageSize = 10,
    SortDirection SortDirection = SortDirection.Desc,
    Status? Status = null,
    SortOption SortOption = SortOption.CreatedAt) : PaginationListRequest(PageNumber, PageSize, SortDirection)
{
    public Query ToQuery() =>
        new(PageNumber, PageSize, SortDirection)
        {
            Status = Status,
            SortOption = SortOption
        };
}
