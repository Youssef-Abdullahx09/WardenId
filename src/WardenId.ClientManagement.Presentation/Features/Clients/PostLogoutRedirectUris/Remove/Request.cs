using WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Remove;

namespace WardenId.ClientManagement.Presentation.Features.Clients.PostLogoutRedirectUris.Remove;

public sealed record Request(string Uri)
{
    public Command ToCommand(Guid tenantId, Guid clientId, Guid actorId) => new(tenantId, clientId, Uri, actorId);
}
