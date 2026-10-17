using FluentResults;
using MediatR;

namespace WardenId.Identity.Integration.Command.Features.Users.RemoveMfa;

/// <summary>
/// Checks a current code (TOTP or recovery code), then removes all of the user's factors and recovery codes.
/// Refused when the tenant's MFA policy requires MFA.
/// </summary>
public sealed record Command(Guid TenantId, Guid UserId, string Code) : IRequest<Result>;
