using FluentResults;
using MediatR;

namespace WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;

/// <summary>
/// Checks a TOTP code from any enabled factor, or else a single-use recovery code. A wrong code counts as a
/// failed sign-in towards the tenant's lockout policy. Failures carry a <see cref="SecondFactorFailure"/> tag.
/// </summary>
public sealed record Command(Guid TenantId, Guid UserId, string Code) : IRequest<Result<SecondFactorVerified>>;

public sealed record SecondFactorVerified(SecondFactorMethod Method, int RemainingRecoveryCodes);

public enum SecondFactorMethod
{
    Totp = 1,
    RecoveryCode = 2
}
