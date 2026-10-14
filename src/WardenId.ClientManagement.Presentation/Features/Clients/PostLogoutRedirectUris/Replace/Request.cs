using WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Replace;

namespace WardenId.ClientManagement.Presentation.Features.Clients.PostLogoutRedirectUris.Replace;

public sealed record Request(IReadOnlyCollection<string> Uris)
{
    public Command ToCommand(Guid tenantId, Guid clientId) => new(tenantId, clientId, Uris);
}
