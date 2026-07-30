using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Unlock;

public sealed record Command(Guid TenantId, Guid UserId, Guid ActorUserId) : IRequest<Result>;
