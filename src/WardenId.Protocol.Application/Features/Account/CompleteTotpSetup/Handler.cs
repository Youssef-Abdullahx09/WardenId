using FluentResults;
using MediatR;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Domain;
using IdentityCompleteTotpEnrollmentCommand = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.Command;

namespace WardenId.Protocol.Application.Features.Account.CompleteTotpSetup;

/// <summary>
/// Enables the new factor. When it replaced an enabled one, every other session of the user in this tenant ends
/// (a stolen session must not outlive the authenticator it was signed in with), keeping this one; a first setup
/// ends nothing. A wrong code is audited as a failed step-up (not a failed sign-in: the user is signed in) and gets
/// the same message as any wrong code.
/// </summary>
public sealed class Handler(ISender sender, IAuthAuditTrail audit, TimeProvider timeProvider)
    : IRequestHandler<Command, Result<IReadOnlyList<string>>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new IdentityCompleteTotpEnrollmentCommand(request.TenantId, request.UserId, request.FactorId, request.Code),
            cancellationToken);
        if (result.IsFailed)
        {
            await audit.RecordAsync(UserChangeAudit.StepUpFailed(
                request.TenantId, request.UserId, OutcomeReason.InvalidSecondFactor, timeProvider.GetUtcNow(), request.Sid));
            return Result.Fail(ProtocolErrors.InvalidSecondFactor());
        }

        var replaced = result.Value.ReplacedFactor;
        await audit.RecordAsync(UserChangeAudit.Changed(
            replaced ? SecurityEvent.AuthenticatorReplaced : SecurityEvent.TotpEnrolled,
            request.TenantId, request.UserId, timeProvider.GetUtcNow(), request.Sid));

        if (replaced)
            await OtherSessions.EndAsync(sender, request.TenantId, request.UserId, request.Sid, cancellationToken);

        return Result.Ok(result.Value.RecoveryCodes);
    }
}
