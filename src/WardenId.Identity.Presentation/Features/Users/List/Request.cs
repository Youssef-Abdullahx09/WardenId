using WardenId.Identity.Application.Features.Users.List;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Presentation.Features.Users.List;

// The sibling namespace WardenId.Identity.Query would otherwise shadow the imported type.
using Query = WardenId.Identity.Application.Features.Users.List.Query;

public sealed record Request(
    int PageNumber,
    int PageSize,
    UserStatus? Status,
    string? Search,
    string? Sort)
{
    public Query ToQuery(Guid tenantId) => new(tenantId, PageNumber, PageSize, Status, Search, Sort);
}
