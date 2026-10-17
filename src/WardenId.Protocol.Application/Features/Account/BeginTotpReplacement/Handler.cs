using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Application.Features.Authorize.BeginTotpEnrollment;
using WardenId.Protocol.Domain;
using IdentityBeginTotpReplacementCommand = WardenId.Identity.Integration.Command.Features.Users.BeginTotpReplacement.Command;

namespace WardenId.Protocol.Application.Features.Account.BeginTotpReplacement;

/// <summary>
/// Any failure is answered like a wrong code; Identity counts wrong codes towards lockout. A failed code check is
/// audited as a failed step-up. Nothing changes yet, so a success isn't audited: CompleteTotpSetup records the
/// replacement.
/// </summary>
public sealed class Handler(ISender sender, IAuthAuditTrail audit, TimeProvider timeProvider)
    : IRequestHandler<Command, Result<StartedTotpEnrollment>>
{
    public async Task<Result<StartedTotpEnrollment>> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new IdentityBeginTotpReplacementCommand(request.TenantId, request.UserId, request.Code, request.Issuer),
            cancellationToken);
        if (result.IsSuccess)
            return Result.Ok(new StartedTotpEnrollment(result.Value.FactorId, result.Value.Secret, result.Value.OtpAuthUri));

        if (await SecondFactorAuditReason.OfAsync(sender, request.TenantId, request.UserId, result.Errors, cancellationToken)
            is { } reason)
            await audit.RecordAsync(UserChangeAudit.StepUpFailed(
                request.TenantId, request.UserId, reason, timeProvider.GetUtcNow(), request.Sid));
        return Result.Fail(ProtocolErrors.InvalidSecondFactor());
    }
}
