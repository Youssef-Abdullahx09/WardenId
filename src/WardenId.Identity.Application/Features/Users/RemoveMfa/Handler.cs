using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using Command = WardenId.Identity.Integration.Command.Features.Users.RemoveMfa.Command;

namespace WardenId.Identity.Application.Features.Users.RemoveMfa;

/// <summary>
/// The user's own removal of their MFA once a current code (TOTP or recovery code) checks out, in the same save as
/// the code's use (see <see cref="SecondFactorCheck"/>). Refused before any code is checked while the tenant's
/// policy requires MFA, so the refusal neither uses up a code nor counts as a failed attempt.
/// </summary>
public sealed class Handler(
    IUserRepository repository,
    IdentityPolicyResolver policies,
    ITotpSecretProtector secretProtector,
    TimeProvider timeProvider) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        if (MfaPolicyEvaluator.IsMfaRequired(policy.Mfa)) return Result.Fail(UserErrors.MfaRequiredCannotRemove());

        var result = await new SecondFactorCheck(repository, secretProtector, timeProvider).RunAsync(
            request.TenantId,
            request.UserId,
            request.Code,
            policy,
            allowRecoveryCode: true,
            (user, _) => user.RemoveMfa(policy.Mfa).ToResult(true),
            cancellationToken);

        return result.ToResult();
    }
}
