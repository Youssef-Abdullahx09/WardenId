using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Update;

/// <param name="Email">The new email; null leaves it unchanged. A change has to be verified again.</param>
/// <param name="Username">The new username; null leaves it unchanged.</param>
public sealed record Command(
    Guid TenantId,
    Guid UserId,
    string? Email,
    string? Username,
    Guid ActorUserId) : IRequest<Result>;
