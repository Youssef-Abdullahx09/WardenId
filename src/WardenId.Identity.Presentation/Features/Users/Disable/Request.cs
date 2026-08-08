using WardenId.Identity.Application.Features.Users.Disable;

namespace WardenId.Identity.Presentation.Features.Users.Disable;

public sealed record Request(string Reason)
{
    public Command ToCommand(Guid tenantId, Guid userId, Guid actorUserId) => new(tenantId, userId, Reason, actorUserId);
}
