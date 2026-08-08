using WardenId.Identity.Application.Features.Users.RequiredActions.Add;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Presentation.Features.Users.RequiredActions.Add;

public sealed record Request(RequiredAction Action)
{
    public Command ToCommand(Guid tenantId, Guid userId, Guid actorUserId) => new(tenantId, userId, Action, actorUserId);
}
