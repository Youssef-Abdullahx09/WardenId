using FluentResults;
using MediatR;

namespace WardenId.Identity.Integration.Command.Features.Users.RegenerateRecoveryCodes;

/// <summary>
/// Checks a current TOTP code (not a recovery code), then replaces all recovery codes with a new set, returned once.
/// A wrong code is handled like VerifySecondFactor (lockout, replay) and tagged with SecondFactorFailure.
/// </summary>
public sealed record Command(Guid TenantId, Guid UserId, string Code) : IRequest<Result<IReadOnlyList<string>>>;
