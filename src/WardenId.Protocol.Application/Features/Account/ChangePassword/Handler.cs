using FluentResults;
using MediatR;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Application.Features.Authorize;
using AuthenticationFailure = WardenId.Identity.Integration.Command.Features.Users.Authenticate.AuthenticationFailure;
using IdentityChangeOwnPasswordCommand = WardenId.Identity.Integration.Command.Features.Users.ChangeOwnPassword.Command;

namespace WardenId.Protocol.Application.Features.Account.ChangePassword;

/// <summary>
/// Identity checks the current password (a wrong one counts as a failed sign-in) and ends the user's sessions on a
/// change. Audited here: the change, or a refused current password (tagged by Identity) as a failed step-up. A new
/// password the policy refuses isn't a failed check and isn't audited.
/// </summary>
public sealed class Handler(ISender sender, IAuthAuditTrail audit, TimeProvider timeProvider) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new IdentityChangeOwnPasswordCommand(request.TenantId, request.UserId, request.CurrentPassword, request.NewPassword),
            cancellationToken);

        if (result.IsSuccess)
            await audit.RecordAsync(UserChangeAudit.Changed(
                SecurityEvent.PasswordChanged, request.TenantId, request.UserId, timeProvider.GetUtcNow(), request.Sid));
        else if (AuthenticationFailure.Read(result.Errors) is { } failure)
            await audit.RecordAsync(UserChangeAudit.StepUpFailed(
                request.TenantId, request.UserId, SignInAudit.ReasonOf(failure.Reason), timeProvider.GetUtcNow(), request.Sid));

        return result;
    }
}
