namespace WardenId.Identity.Integration.Command.Features.Users.Authenticate;

public sealed record AuthenticatedUser(
    Guid UserId,
    string Email,
    string Username,
    UserStatus Status,
    IReadOnlyCollection<RequiredAction> PendingActions);
