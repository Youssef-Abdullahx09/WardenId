using WardenId.ClientManagement.Application.Features.Clients.List;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Presentation.Features.Clients.List;

public sealed record Request(
    int PageNumber,
    int PageSize,
    bool? Enabled,
    ClientType? ClientType)
{
    public Query ToQuery(Guid tenantId) => new(tenantId, PageNumber, PageSize, Enabled, ClientType);
}
