using WardenId.ClientManagement.Application.Features.Clients.RedirectUris.Add;

namespace WardenId.ClientManagement.Presentation.Features.Clients.RedirectUris.Add;

public sealed record Request(string Uri)
{
    public Command ToCommand(Guid tenantId, Guid clientId) => new(tenantId, clientId, Uri);
}
