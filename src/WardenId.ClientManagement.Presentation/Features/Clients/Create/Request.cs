using WardenId.ClientManagement.Application.Features.Clients.Create;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Create;

public sealed record Request(
    ClientType ClientType,
    string DisplayName,
    GrantType GrantType,
    ClientAuthenticationMethod ClientAuthenticationMethod,
    bool IsFirstParty,
    bool ConsentRequired,
    string? Description)
{
    public Command ToCommand(Guid tenantId, Guid actorId) => new(
        tenantId, ClientType, DisplayName, GrantType, ClientAuthenticationMethod,
        IsFirstParty, ConsentRequired, Description, actorId);
}
