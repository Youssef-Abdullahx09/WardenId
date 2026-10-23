using WardenId.Identity.Integration.Command.Features.Users.Authenticate;

namespace WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;

/// <param name="FirstName">Null when the user has none.</param>
/// <param name="LastName">Null when the user has none.</param>
public sealed record SignInUser(
    Guid UserId,
    string Email,
    string Username,
    UserStatus Status,
    IReadOnlyCollection<RequiredAction> PendingActions,
    string? FirstName = null,
    string? LastName = null);
