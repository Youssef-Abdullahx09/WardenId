using System.Security.Cryptography;
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Mfa;
using WardenId.Identity.Domain;
using Command = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.Command;
using TotpEnrollment = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.TotpEnrollment;

namespace WardenId.Identity.Application.Features.Users.BeginTotpEnrollment;

/// <summary>
/// Only a user who has to enroll (see <see cref="MfaRequirement.MustEnroll"/>) can start an enrollment, so a
/// password alone never lets anyone add a factor to an account that is already protected by one.
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

        if (!MfaRequirement.MustEnroll(user, policyResult.Value.Mfa))
            return Result.Fail(UserErrors.MfaEnrollmentNotRequired());

        var secret = Totp.GenerateSecret();
        try
        {
            var factor = MfaFactor.Create(MfaFactorType.Totp, secretProtector.Protect(secret), timeProvider.GetUtcNow());

            var beginResult = user.BeginMfaEnrollment(factor);
            if (beginResult.IsFailed) return Result.Fail(beginResult.Errors);

            var saveResult = await repository.UpdateAsync(user, user.Id, cancellationToken);
            if (saveResult.IsFailed) return Result.Fail(saveResult.Errors);

            var base32Secret = Totp.ToBase32(secret);
            var uri = Totp.OtpAuthUri(request.Issuer.Trim(), user.Email.Value, base32Secret);
            return Result.Ok(new TotpEnrollment(factor.Id, base32Secret, uri));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }
}
