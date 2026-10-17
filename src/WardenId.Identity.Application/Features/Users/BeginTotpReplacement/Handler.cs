using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using Command = WardenId.Identity.Integration.Command.Features.Users.BeginTotpReplacement.Command;
using TotpEnrollment = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.TotpEnrollment;

namespace WardenId.Identity.Application.Features.Users.BeginTotpReplacement;

/// <summary>
/// Starts a new TOTP enrollment once a current code (TOTP or recovery code) checks out, in the same save as the
/// code's use (see <see cref="SecondFactorCheck"/>). The current factors stay enabled until CompleteTotpEnrollment
/// enables the new one and removes them, so an abandoned replacement leaves the account as it was.
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

        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);

        return await new SecondFactorCheck(repository, secretProtector, timeProvider).RunAsync(
            request.TenantId,
            request.UserId,
            request.Code,
            policyResult.Value,
            allowRecoveryCode: true,
            (user, _) => TotpEnrollmentStart.Begin(user, request.Issuer, secretProtector, timeProvider.GetUtcNow()),
            cancellationToken);
    }
}
