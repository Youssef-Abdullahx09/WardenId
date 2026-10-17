using FluentResults;
using MediatR;
using IdentityChangeOwnPasswordCommand = WardenId.Identity.Integration.Command.Features.Users.ChangeOwnPassword.Command;

namespace WardenId.Protocol.Application.Features.Account.ChangePassword;

/// <summary>Identity checks the current password (a wrong one counts as a failed sign-in) and audits the change.</summary>
public sealed class Handler(ISender sender) : IRequestHandler<Command, Result>
{
    public Task<Result> Handle(Command request, CancellationToken cancellationToken) =>
        sender.Send(
            new IdentityChangeOwnPasswordCommand(request.TenantId, request.UserId, request.CurrentPassword, request.NewPassword),
            cancellationToken);
}
