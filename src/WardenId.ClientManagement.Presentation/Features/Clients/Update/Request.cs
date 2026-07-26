using WardenId.ClientManagement.Application.Features.Clients.Update;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Update;

public sealed record Request(
    string? DisplayName,
    bool? Enabled,
    bool? ConsentRequired,
    int? AccessTokenSeconds,
    int? IdTokenSeconds,
    int? RefreshTokenSeconds,
    bool ClearTokenLifetimeOverrides)
{
    public Command ToCommand(Guid tenantId, Guid clientId) => new(
        tenantId, clientId, DisplayName, Enabled, ConsentRequired,
        AccessTokenSeconds, IdTokenSeconds, RefreshTokenSeconds, ClearTokenLifetimeOverrides);
}
