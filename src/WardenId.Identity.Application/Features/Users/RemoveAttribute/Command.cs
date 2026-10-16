using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.RemoveAttribute;

public sealed record Command(
    Guid TenantId,
    Guid UserId,
    string Key,
    Guid ActorUserId) : IRequest<Result>;
