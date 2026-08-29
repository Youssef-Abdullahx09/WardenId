using WardenId.Protocol.Application.Features.Login;

namespace WardenId.Protocol.Presentation.Features.Login;

public sealed record Request(string EmailOrUsername, string Password)
{
    public Command ToCommand(Guid tenantId) => new(tenantId, EmailOrUsername, Password);
}
