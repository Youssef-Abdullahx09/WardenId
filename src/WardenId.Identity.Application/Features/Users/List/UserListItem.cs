using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.List;

public sealed record UserListItem(
    Guid Id,
    string Email,
    string Username,
    string? FirstName,
    string? LastName,
    UserStatus Status,
    DateTimeOffset CreatedAt);
