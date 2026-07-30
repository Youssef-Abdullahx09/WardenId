using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.VerifyMfaFactor;

public sealed record Command(Guid TenantId, Guid UserId, Guid FactorId) : IRequest<Result>;
