using WardenId.ClientManagement.Application.Features.Clients.RedirectUris.Remove;

namespace WardenId.ClientManagement.Presentation.Features.Clients.RedirectUris.Remove;

public sealed record Request(string Uri)
{
    public Command ToCommand(Guid tenantId, Guid clientId, Guid actorId) => new(tenantId, clientId, Uri, actorId);
}
