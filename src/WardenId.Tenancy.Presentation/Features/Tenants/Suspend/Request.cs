using WardenId.Tenancy.Application.Features.Tenants.Suspend;

namespace WardenId.Tenancy.Presentation.Features.Tenants.Suspend;

public sealed record Request(string Reason)
{
    public Command ToCommand(Guid tenantId, Guid userId) => new(tenantId, Reason, userId);
}
