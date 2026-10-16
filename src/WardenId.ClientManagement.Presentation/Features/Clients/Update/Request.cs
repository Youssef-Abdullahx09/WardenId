using WardenId.ClientManagement.Application.Features.Clients.Update;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Update;

/// <summary>
/// Body of <c>PATCH .../clients/{clientId}</c>. Omitted (null) fields stay unchanged; <c>"description": ""</c>
/// clears the description. Client type, PKCE and authentication method cannot be changed after create.
/// </summary>
public sealed record Request(
    string? DisplayName,
    string? Description,
    bool? Enabled,
    bool? IsFirstParty,
    bool? ConsentRequired,
    GrantType? GrantType,
    int? AccessTokenSeconds,
    int? IdTokenSeconds,
    int? RefreshTokenSeconds,
    bool ClearTokenLifetimeOverrides)
{
    public Command ToCommand(Guid tenantId, Guid clientId, Guid actorId) => new(
        tenantId, clientId, DisplayName, Description, Enabled, IsFirstParty, ConsentRequired, GrantType,
        AccessTokenSeconds, IdTokenSeconds, RefreshTokenSeconds, ClearTokenLifetimeOverrides, actorId);
}
