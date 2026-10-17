using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using Command = WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor.Command;

namespace WardenId.Identity.Application.Features.Users.VerifySecondFactor;

/// <summary>
/// The code step of a sign-in, after the password: a TOTP code or a recovery code, checked by
/// <see cref="SecondFactorCheck"/> (lockout counting, replay, concurrency retries).
/// </summary>
public sealed class Handler(
    IUserRepository repository,
    IdentityPolicyResolver policies,
    ITotpSecretProtector secretProtector,
    TimeProvider timeProvider) : IRequestHandler<Command, Result<SecondFactorVerified>>
{
    public async Task<Result<SecondFactorVerified>> Handle(Command request, CancellationToken cancellationToken)
    {
        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);

        return await new SecondFactorCheck(repository, secretProtector, timeProvider).RunAsync(
            request.TenantId,
            request.UserId,
            request.Code,
            policyResult.Value,
            allowRecoveryCode: true,
            (user, method) => Result.Ok(new SecondFactorVerified(method, user.RemainingRecoveryCodeCount)),
            cancellationToken);
    }
}
