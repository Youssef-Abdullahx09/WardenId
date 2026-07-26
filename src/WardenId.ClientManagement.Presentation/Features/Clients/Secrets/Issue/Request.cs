using WardenId.ClientManagement.Application.Features.Clients.Secrets.Issue;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Secrets.Issue;

public sealed record Request(
    string Label,
    int? OverlapDays)
{
    public Command ToCommand(Guid tenantId, Guid clientId) => new(tenantId, clientId, Label, OverlapDays);
}
