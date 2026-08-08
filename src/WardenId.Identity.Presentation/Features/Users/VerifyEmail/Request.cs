using WardenId.Identity.Application.Features.Users.VerifyEmail;

namespace WardenId.Identity.Presentation.Features.Users.VerifyEmail;

public sealed record Request(string RawToken)
{
    public Command ToCommand(Guid tenantId, Guid userId) => new(tenantId, userId, RawToken);
}
