using FluentResults;
using MediatR;
using WardenId.Protocol.Domain;
using AuthenticateCommand = WardenId.Identity.Integration.Command.Features.Users.Authenticate.Command;
using RequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Protocol.Application.Features.Authorize.SignIn;

public sealed class Handler(ISender sender) : IRequestHandler<Command, Result<SignInOutcome>>
{
    // Most fundamental first: an unverified mailbox makes the other actions moot.
    private static readonly (RequiredAction Action, SignInBlockReason Reason)[] BlockPriority =
    [
        (RequiredAction.VerifyEmail, SignInBlockReason.VerifyEmail),
        (RequiredAction.ResetPassword, SignInBlockReason.ResetPassword),
        (RequiredAction.AcceptTos, SignInBlockReason.AcceptTos),
        (RequiredAction.EnrollMfa, SignInBlockReason.EnrollMfa)
    ];

    public async Task<Result<SignInOutcome>> Handle(Command request, CancellationToken cancellationToken)
    {
        var authResult = await sender.Send(
            new AuthenticateCommand(request.TenantId, request.EmailOrUsername, request.Password), cancellationToken);
        if (authResult.IsFailed) return Result.Fail(ProtocolErrors.InvalidCredentials());

        var user = authResult.Value;
        if (user.Status is UserStatus.Disabled or UserStatus.Locked or UserStatus.SoftDeleted)
            return Result.Fail(ProtocolErrors.InvalidCredentials());

        foreach (var (action, reason) in BlockPriority)
        {
            if (user.PendingActions.Contains(action)) return Result.Ok(new SignInOutcome(user.UserId, reason));
        }

        return user.Status == UserStatus.PendingVerification
            ? Result.Ok(new SignInOutcome(user.UserId, SignInBlockReason.PendingVerification))
            : Result.Ok(new SignInOutcome(user.UserId, BlockedReason: null));
    }
}
