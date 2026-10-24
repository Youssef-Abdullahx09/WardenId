using WardenId.ClientManagement.Query.Contracts.Features.Clients;
using WardenId.ClientManagement.Query.Contracts.Features.Clients.List;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.ClientManagement.Presentation.Features.Clients.List;

// The sibling namespace WardenId.ClientManagement.Query would otherwise shadow the imported type.
using Query = WardenId.ClientManagement.Application.Features.Clients.List.Query;

public sealed record Request(
    int PageNumber = 1,
    int PageSize = 10,
    SortDirection SortDirection = SortDirection.Desc,
    bool? Enabled = null,
    ClientType? ClientType = null,
    SortOption SortOption = SortOption.CreatedAt) : PaginationListRequest(PageNumber, PageSize, SortDirection)
{
    public Query ToQuery(Guid tenantId) =>
        new(tenantId, PageNumber, PageSize, SortDirection)
        {
            Enabled = Enabled,
            ClientType = ClientType,
            SortOption = SortOption
        };
}
