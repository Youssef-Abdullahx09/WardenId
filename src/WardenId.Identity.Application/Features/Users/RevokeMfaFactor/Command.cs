using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.RevokeMfaFactor;

public sealed record Command(
    Guid TenantId,
    Guid UserId,
    Guid FactorId,
    Guid ActorUserId,
    bool TargetUserIsAdmin) : IRequest<Result>;
