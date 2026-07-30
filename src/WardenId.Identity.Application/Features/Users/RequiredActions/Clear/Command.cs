using FluentResults;
using MediatR;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RequiredActions.Clear;

public sealed record Command(Guid TenantId, Guid UserId, RequiredAction Action, Guid ActorUserId) : IRequest<Result>;
