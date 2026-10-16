using WardenId.ClientManagement.Application.Features.Clients.BackchannelLogoutUri.Set;

namespace WardenId.ClientManagement.Presentation.Features.Clients.BackchannelLogoutUri.Set;

/// <summary>Body of <c>PUT .../backchannel-logout-uri</c>; <c>{ "uri": null }</c> clears the URI.</summary>
public sealed record Request(string? Uri)
{
    public Command ToCommand(Guid tenantId, Guid clientId, Guid actorId) => new(tenantId, clientId, Uri, actorId);
}
