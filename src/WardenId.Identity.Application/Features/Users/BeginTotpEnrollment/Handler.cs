using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using Command = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.Command;
using TotpEnrollment = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.TotpEnrollment;

namespace WardenId.Identity.Application.Features.Users.BeginTotpEnrollment;

/// <summary>
/// Only a user who may enroll without a code (see <see cref="MfaRequirement.MayEnrollWithoutCode"/>) can start an
/// enrollment here, so a password alone never lets anyone add a factor to an account that is already protected by
/// one. Replacing an enabled factor takes a current code: BeginTotpReplacement.
/// </summary>
public sealed class Handler(
    IUserRepository repository,
    IdentityPolicyResolver policies,
    ITotpSecretProtector secretProtector,
    TimeProvider timeProvider) : IRequestHandler<Command, Result<TotpEnrollment>>
{
    public async Task<Result<TotpEnrollment>> Handle(Command request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Issuer)) return Result.Fail(UserErrors.TotpIssuerRequired());

        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);
        var user = userResult.Value;

        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);

        if (!MfaRequirement.MayEnrollWithoutCode(user, policyResult.Value.Mfa))
            return Result.Fail(UserErrors.MfaEnrollmentNotRequired());

        var beginResult = TotpEnrollmentStart.Begin(user, request.Issuer, secretProtector, timeProvider.GetUtcNow());
        if (beginResult.IsFailed) return beginResult;

        var saveResult = await repository.UpdateAsync(user, user.Id, cancellationToken);
        if (saveResult.IsFailed) return Result.Fail(saveResult.Errors);

        return beginResult;
    }
}
