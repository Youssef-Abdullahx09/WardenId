using WardenId.ClientManagement.Application.Features.Clients.RedirectUris.Replace;

namespace WardenId.ClientManagement.Presentation.Features.Clients.RedirectUris.Replace;

public sealed record Request(IReadOnlyCollection<string> Uris)
{
    public Command ToCommand(Guid tenantId, Guid clientId, Guid actorId) => new(tenantId, clientId, Uris, actorId);
}
