using WardenId.Identity.Application.Features.Users.ResetPassword;

namespace WardenId.Identity.Presentation.Features.Users.ResetPassword;

public sealed record Request(
    string Email,
    string RawToken,
    string NewPassword)
{
    public Command ToCommand(Guid tenantId) => new(tenantId, Email, RawToken, NewPassword);
}
