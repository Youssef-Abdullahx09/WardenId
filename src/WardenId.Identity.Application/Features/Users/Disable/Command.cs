using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Disable;

public sealed record Command(Guid TenantId, Guid UserId, string Reason, Guid ActorUserId) : IRequest<Result>;
