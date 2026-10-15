using FluentResults;
using MediatR;
using GetForAuthenticationQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication.Query;

namespace WardenId.Protocol.Application.Features.BackchannelLogout.Deliver;

/// <summary>
/// Delivers one queued back-channel logout (spec D10). The client is looked up again at delivery time,
/// so a client disabled, deleted or unregistered from back-channel logout after the session ended is
/// skipped. The outcome is returned rather than thrown; the CAP subscriber decides whether to retry.
/// </summary>
public sealed class Handler(ISender sender, ILogoutTokenFactory tokenFactory, IBackchannelLogoutSender backchannelSender)
    : IRequestHandler<Command, Result<DeliveryOutcome>>
{
    public async Task<Result<DeliveryOutcome>> Handle(Command request, CancellationToken cancellationToken)
    {
        var message = request.Message;

        var client = await sender.Send(new GetForAuthenticationQuery(message.TenantId, message.ClientId), cancellationToken);
        if (client.IsFailed || !client.Value.Enabled || client.Value.BackchannelLogoutUri is not { } registeredUri)
            return Result.Ok(DeliveryOutcome.Skipped);

        // ClientManagement validated it on write; an unparsable stored value can never succeed.
        if (!Uri.TryCreate(registeredUri, UriKind.Absolute, out var target))
            return Result.Ok(DeliveryOutcome.Rejected);

        var logoutToken = await tokenFactory.CreateAsync(
            message.TenantId, message.ClientId, message.SubjectId, message.Sid, cancellationToken);
        // No active signing key (e.g. tenant mid-rotation): retry later rather than drop the notification.
        if (logoutToken.IsFailed)
            return Result.Ok(DeliveryOutcome.TransientFailure);

        return Result.Ok(await backchannelSender.SendAsync(target, logoutToken.Value, cancellationToken));
    }
}
