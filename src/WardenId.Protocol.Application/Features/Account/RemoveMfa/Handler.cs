using FluentResults;
using MediatR;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Domain;
using IdentityRemoveMfaCommand = WardenId.Identity.Integration.Command.Features.Users.RemoveMfa.Command;

namespace WardenId.Protocol.Application.Features.Account.RemoveMfa;

/// <summary>
/// A failed code check (tagged by Identity) gets the one wrong-code message and is audited as a failed step-up; any
/// other refusal, such as a policy that requires MFA, gets a fixed message that reveals nothing about the code and
/// isn't audited (no code was checked). A removal is audited and ends every other session of the user in this
/// tenant, keeping this one.
/// </summary>
public sealed class Handler(ISender sender, IAuthAuditTrail audit, TimeProvider timeProvider) : IRequestHandler<Command, Result>
{
    public const string RefusedMessage = "Two-step verification can't be turned off for your account.";

    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new IdentityRemoveMfaCommand(request.TenantId, request.UserId, request.Code), cancellationToken);
        if (result.IsSuccess)
        {
            await audit.RecordAsync(UserChangeAudit.Changed(
                SecurityEvent.MfaRemoved, request.TenantId, request.UserId, timeProvider.GetUtcNow(), request.Sid));
            await OtherSessions.EndAsync(sender, request.TenantId, request.UserId, request.Sid, cancellationToken);
            return Result.Ok();
        }

        if (SecondFactorFailure.Read(result.Errors) is null)
            return Result.Fail(ProtocolErrors.Validation(RefusedMessage));

        if (await SecondFactorAuditReason.OfAsync(sender, request.TenantId, request.UserId, result.Errors, cancellationToken)
            is { } reason)
            await audit.RecordAsync(UserChangeAudit.StepUpFailed(
                request.TenantId, request.UserId, reason, timeProvider.GetUtcNow(), request.Sid));
        return Result.Fail(ProtocolErrors.InvalidSecondFactor());
    }
}
