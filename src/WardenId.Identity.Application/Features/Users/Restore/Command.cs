using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Restore;

public sealed record Command(Guid TenantId, Guid UserId, Guid ActorUserId) : IRequest<Result>;
