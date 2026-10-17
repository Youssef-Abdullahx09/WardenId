using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.ResetMfa;

/// <summary>An admin removes every MFA factor and recovery code of a user, who enrolls again at their next sign-in.</summary>
public sealed record Command(Guid TenantId, Guid UserId, Guid ActorUserId) : IRequest<Result>;
