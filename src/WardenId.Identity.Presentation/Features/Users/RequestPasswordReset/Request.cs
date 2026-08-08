using WardenId.Identity.Application.Features.Users.RequestPasswordReset;

namespace WardenId.Identity.Presentation.Features.Users.RequestPasswordReset;

public sealed record Request(string Email)
{
    public Command ToCommand(Guid tenantId) => new(tenantId, Email);
}
