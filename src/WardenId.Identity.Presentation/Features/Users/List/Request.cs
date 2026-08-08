using WardenId.Identity.Application.Features.Users.List;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Presentation.Features.Users.List;

public sealed record Request(
    int PageNumber,
    int PageSize,
    UserStatus? Status)
{
    public Query ToQuery(Guid tenantId) => new(tenantId, PageNumber, PageSize, Status);
}
