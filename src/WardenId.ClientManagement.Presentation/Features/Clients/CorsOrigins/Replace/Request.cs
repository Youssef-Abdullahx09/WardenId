using WardenId.ClientManagement.Application.Features.Clients.CorsOrigins.Replace;

namespace WardenId.ClientManagement.Presentation.Features.Clients.CorsOrigins.Replace;

public sealed record Request(IReadOnlyCollection<string> Origins)
{
    public Command ToCommand(Guid tenantId, Guid clientId, Guid actorId) => new(tenantId, clientId, Origins, actorId);
}
