using WardenId.Identity.Application.Features.Users.Create;

namespace WardenId.Identity.Presentation.Features.Users.Create;

public sealed record Request(string Email, string Username, string TemporaryPassword, bool RequirePasswordChange = true)
{
    public Command ToCommand(Guid tenantId, Guid actorUserId) =>
        new(tenantId, Email, Username, TemporaryPassword, RequirePasswordChange, actorUserId);
}
