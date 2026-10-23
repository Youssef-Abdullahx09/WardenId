using WardenId.Identity.Application.Features.Users.Create;

namespace WardenId.Identity.Presentation.Features.Users.Create;

/// <param name="FirstName">Optional; null, missing or blank for none.</param>
/// <param name="LastName">Optional; null, missing or blank for none.</param>
public sealed record Request(
    string Email,
    string Username,
    string TemporaryPassword,
    bool RequirePasswordChange = true,
    string? FirstName = null,
    string? LastName = null)
{
    public Command ToCommand(Guid tenantId, Guid actorUserId) =>
        new(tenantId, Email, Username, FirstName, LastName, TemporaryPassword, RequirePasswordChange, actorUserId);
}
