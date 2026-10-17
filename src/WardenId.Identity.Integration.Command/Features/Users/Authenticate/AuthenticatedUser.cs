namespace WardenId.Identity.Integration.Command.Features.Users.Authenticate;

/// <param name="HasSecondFactor">The user has an enabled MFA factor: sign-in needs a code step.</param>
/// <param name="PendingActions">
/// Includes <see cref="RequiredAction.EnrollMfa"/> when the tenant's MFA policy requires MFA and the user has no
/// enabled factor, even if the action isn't stored on the user.
/// </param>
public sealed record AuthenticatedUser(
    Guid UserId,
    string Email,
    string Username,
    UserStatus Status,
    IReadOnlyCollection<RequiredAction> PendingActions,
    bool HasSecondFactor);
