using WardenId.Identity.Application.Features.Users.ChangePassword;

namespace WardenId.Identity.Presentation.Features.Users.ChangePassword;

public sealed record Request(
    string CurrentPassword,
    string NewPassword)
{
    public Command ToCommand(Guid tenantId, Guid userId) => new(tenantId, userId, CurrentPassword, NewPassword);
}
