using WardenId.Identity.Application.Features.Users.Update;

namespace WardenId.Identity.Presentation.Features.Users.Update;

/// <param name="Email">The new email; null or missing leaves it unchanged.</param>
/// <param name="Username">The new username; null or missing leaves it unchanged.</param>
public sealed record Request(string? Email, string? Username)
{
    public Command ToCommand(Guid tenantId, Guid userId, Guid actorUserId) =>
        new(tenantId, userId, Email, Username, actorUserId);
}
