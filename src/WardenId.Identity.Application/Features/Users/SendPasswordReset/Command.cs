using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.SendPasswordReset;

/// <summary>An admin sends a user the same password-reset email the user can request themselves.</summary>
public sealed record Command(Guid TenantId, Guid UserId, Guid ActorUserId) : IRequest<Result>;
