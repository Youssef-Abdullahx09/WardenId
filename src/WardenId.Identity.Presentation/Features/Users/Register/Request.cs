using WardenId.Identity.Application.Features.Users.Register;

namespace WardenId.Identity.Presentation.Features.Users.Register;

public sealed record Request(
    string Email,
    string Username,
    string Password)
{
    public Command ToCommand(Guid tenantId) => new(tenantId, Email, Username, Password);
}
