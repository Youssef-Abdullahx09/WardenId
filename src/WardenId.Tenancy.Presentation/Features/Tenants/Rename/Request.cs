using WardenId.Tenancy.Application.Features.Tenants.Rename;

namespace WardenId.Tenancy.Presentation.Features.Tenants.Rename;

public sealed record Request(string DisplayName)
{
    public Command ToCommand(Guid tenantId, Guid userId) => new(tenantId, DisplayName, userId);
}
