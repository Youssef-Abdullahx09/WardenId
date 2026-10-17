using FluentResults;
using MediatR;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Domain;
using IdentityRegenerateRecoveryCodesCommand = WardenId.Identity.Integration.Command.Features.Users.RegenerateRecoveryCodes.Command;

namespace WardenId.Protocol.Application.Features.Account.RegenerateRecoveryCodes;

/// <summary>
/// Any failure is answered like a wrong code; Identity counts wrong codes towards lockout. Audited: the new codes
/// (never the codes themselves), or a failed code check as a failed step-up.
/// </summary>
public sealed class Handler(ISender sender, IAuthAuditTrail audit, TimeProvider timeProvider)
    : IRequestHandler<Command, Result<IReadOnlyList<string>>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new IdentityRegenerateRecoveryCodesCommand(request.TenantId, request.UserId, request.Code), cancellationToken);
        if (result.IsSuccess)
        {
            await audit.RecordAsync(UserChangeAudit.Changed(
                SecurityEvent.RecoveryCodesRegenerated, request.TenantId, request.UserId, timeProvider.GetUtcNow(), request.Sid));
            return Result.Ok(result.Value);
        }

        if (await SecondFactorAuditReason.OfAsync(sender, request.TenantId, request.UserId, result.Errors, cancellationToken)
            is { } reason)
            await audit.RecordAsync(UserChangeAudit.StepUpFailed(
                request.TenantId, request.UserId, reason, timeProvider.GetUtcNow(), request.Sid));
        return Result.Fail(ProtocolErrors.InvalidSecondFactor());
    }
}
