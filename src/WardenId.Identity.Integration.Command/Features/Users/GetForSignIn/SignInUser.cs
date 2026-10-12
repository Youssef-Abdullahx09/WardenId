using WardenId.Identity.Integration.Command.Features.Users.Authenticate;

namespace WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;

public sealed record SignInUser(
    Guid UserId,
    string Email,
    string Username,
    UserStatus Status,
    IReadOnlyCollection<RequiredAction> PendingActions);
