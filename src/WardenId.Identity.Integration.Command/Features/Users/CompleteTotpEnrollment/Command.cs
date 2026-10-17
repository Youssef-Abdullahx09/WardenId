using FluentResults;
using MediatR;

namespace WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment;

/// <summary>
/// Checks the first code of a factor started by BeginTotpEnrollment, enables it, clears the EnrollMfa required
/// action and issues a fresh set of recovery codes (the old ones stop working).
/// </summary>
public sealed record Command(Guid TenantId, Guid UserId, Guid FactorId, string Code)
    : IRequest<Result<CompletedTotpEnrollment>>;

public sealed record CompletedTotpEnrollment(IReadOnlyList<string> RecoveryCodes);
