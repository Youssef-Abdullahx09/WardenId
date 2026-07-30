using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.List;

public sealed record UserListItem(
    Guid Id,
    string Email,
    string Username,
    UserStatus Status,
    DateTimeOffset CreatedAt);
