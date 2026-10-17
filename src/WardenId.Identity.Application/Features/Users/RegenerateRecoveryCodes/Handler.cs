using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Mfa;
using Command = WardenId.Identity.Integration.Command.Features.Users.RegenerateRecoveryCodes.Command;

namespace WardenId.Identity.Application.Features.Users.RegenerateRecoveryCodes;

/// <summary>
/// Replaces the recovery codes once a current TOTP code checks out, in the same save as the code's use (see
/// <see cref="SecondFactorCheck"/>). A recovery code isn't accepted: one leaked code mustn't buy a fresh set.
/// </summary>
public sealed class Handler(
    IUserRepository repository,
    IdentityPolicyResolver policies,
    ITotpSecretProtector secretProtector,
    TimeProvider timeProvider) : IRequestHandler<Command, Result<IReadOnlyList<string>>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(Command request, CancellationToken cancellationToken)
    {
        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);

        return await new SecondFactorCheck(repository, secretProtector, timeProvider).RunAsync(
            request.TenantId,
            request.UserId,
            request.Code,
            policyResult.Value,
            allowRecoveryCode: false,
            (user, _) =>
            {
                var (rawCodes, codes) = RecoveryCodes.Generate();
                var regenerateResult = user.RegenerateRecoveryCodes(codes);
                return regenerateResult.IsFailed
                    ? Result.Fail<IReadOnlyList<string>>(regenerateResult.Errors)
                    : Result.Ok(rawCodes);
            },
            cancellationToken);
    }
}
