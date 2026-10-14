using WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Add;

namespace WardenId.ClientManagement.Presentation.Features.Clients.PostLogoutRedirectUris.Add;

public sealed record Request(string Uri)
{
    public Command ToCommand(Guid tenantId, Guid clientId) => new(tenantId, clientId, Uri);
}
