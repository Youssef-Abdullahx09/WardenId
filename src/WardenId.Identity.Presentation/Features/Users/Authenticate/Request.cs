using WardenId.Identity.Integration.Command.Features.Users.Authenticate;

namespace WardenId.Identity.Presentation.Features.Users.Authenticate;

public sealed record Request(
    string EmailOrUsername,
    string Password)
{
    public Command ToCommand(Guid tenantId) => new(tenantId, EmailOrUsername, Password);
}
