using WardenId.Identity.Application.Features.Users.Update;

namespace WardenId.Identity.Presentation.Features.Users.Update;

/// <param name="Email">The new email; null or missing leaves it unchanged.</param>
/// <param name="Username">The new username; null or missing leaves it unchanged.</param>
/// <param name="FirstName">The new first name; null or missing leaves it unchanged, empty removes it.</param>
/// <param name="LastName">The new last name; null or missing leaves it unchanged, empty removes it.</param>
public sealed record Request(string? Email, string? Username, string? FirstName = null, string? LastName = null)
{
    public Command ToCommand(Guid tenantId, Guid userId, Guid actorUserId) =>
        new(tenantId, userId, Email, Username, FirstName, LastName, actorUserId);
}
