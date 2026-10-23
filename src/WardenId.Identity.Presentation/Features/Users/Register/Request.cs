using WardenId.Identity.Application.Features.Users.Register;

namespace WardenId.Identity.Presentation.Features.Users.Register;

/// <param name="FirstName">Optional; null, missing or blank for none.</param>
/// <param name="LastName">Optional; null, missing or blank for none.</param>
public sealed record Request(
    string Email,
    string Username,
    string Password,
    string? FirstName = null,
    string? LastName = null)
{
    public Command ToCommand(Guid tenantId) => new(tenantId, Email, Username, FirstName, LastName, Password);
}
