using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Authenticate;

public sealed record AuthenticatedUser(Guid UserId, UserStatus Status, IReadOnlyCollection<RequiredAction> PendingActions);
