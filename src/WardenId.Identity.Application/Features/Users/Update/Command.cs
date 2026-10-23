using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Update;

/// <param name="Email">The new email; null leaves it unchanged. A change has to be verified again.</param>
/// <param name="Username">The new username; null leaves it unchanged.</param>
/// <param name="FirstName">The new first name; null leaves it unchanged, empty or blank removes it.</param>
/// <param name="LastName">The new last name; null leaves it unchanged, empty or blank removes it.</param>
public sealed record Command(
    Guid TenantId,
    Guid UserId,
    string? Email,
    string? Username,
    string? FirstName,
    string? LastName,
    Guid ActorUserId) : IRequest<Result>;
