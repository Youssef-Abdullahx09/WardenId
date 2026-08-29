using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Authenticate;

public sealed record AuthenticatedUser(Guid UserId, string Email, string Username, UserStatus Status, IReadOnlyCollection<RequiredAction> PendingActions);
