using FluentResults;
using MediatR;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Domain;
using IdentityCompleteTotpEnrollmentCommand = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.Command;

namespace WardenId.Protocol.Application.Features.Authorize.CompleteTotpEnrollment;

/// <summary>
/// Enables the factor and audits the enrollment (a replacement when Identity removed an enabled authenticator for
/// it, as when an admin asked a user with a factor to set up a new one). A refused first code is audited as a
/// failed sign-in (the user is mid sign-in) and answered with the same message as a wrong code at the code step.
/// No session exists yet, so none is ended here.
/// </summary>
public sealed class Handler(ISender sender, IAuthAuditTrail audit, TimeProvider timeProvider)
    : IRequestHandler<Command, Result<CompletedTotpEnrollment>>
{
    public async Task<Result<CompletedTotpEnrollment>> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new IdentityCompleteTotpEnrollmentCommand(request.TenantId, request.UserId, request.FactorId, request.Code),
            cancellationToken);
        if (result.IsSuccess)
        {
            await audit.RecordAsync(UserChangeAudit.Changed(
                result.Value.ReplacedFactor ? SecurityEvent.AuthenticatorReplaced : SecurityEvent.TotpEnrolled,
                request.TenantId, request.UserId, timeProvider.GetUtcNow()));
            return Result.Ok(new CompletedTotpEnrollment(result.Value.RecoveryCodes));
        }

        await audit.RecordAsync(SignInAudit.Failed(
            request.TenantId, request.UserId, OutcomeReason.InvalidSecondFactor, timeProvider.GetUtcNow()));
        return Result.Fail(ProtocolErrors.InvalidSecondFactor());
    }
}
