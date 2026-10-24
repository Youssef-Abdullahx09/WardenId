using WardenId.Protocol.Query.Contracts.Features.Sessions.List;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Protocol.Presentation.Features.Sessions.List;

// The sibling namespace WardenId.Protocol.Query would otherwise shadow the imported type.
using Query = WardenId.Protocol.Application.Features.Sessions.List.Query;

public sealed record Request(
    int PageNumber = 1,
    int PageSize = 10,
    SortDirection SortDirection = SortDirection.Desc,
    Guid? UserId = null,
    string? ClientId = null,
    bool IncludeEnded = false,
    SortOption SortOption = SortOption.AuthTime) : PaginationListRequest(PageNumber, PageSize, SortDirection)
{
    public Query ToQuery(Guid tenantId) =>
        new(tenantId, PageNumber, PageSize, SortDirection)
        {
            UserId = UserId,
            ClientId = ClientId,
            IncludeEnded = IncludeEnded,
            SortOption = SortOption
        };
}
