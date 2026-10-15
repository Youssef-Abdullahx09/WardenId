using MediatR;
using Microsoft.Extensions.Logging;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.SharedKernel.Application.CapPublisher;
using DeliverCommand = WardenId.Protocol.Application.Features.BackchannelLogout.Deliver.Command;
using Requested = WardenId.Protocol.Application.Features.BackchannelLogout.BackchannelLogoutRequested;

namespace WardenId.Protocol.Application.Subscribers.BackchannelLogoutRequested;

/// <summary>
/// One delivery per message (spec D10). Only a transient failure throws, so CAP retries that one client alone.
/// A rejection or skip is final and the message is acknowledged.
/// </summary>
public sealed class Subscriber(ISender sender, ILogger<Subscriber> logger) : ICapSubscriber<Message>
{
    // Group is explicit: CAP silently drops one of two subscriptions sharing (topic, group).
    [CapSubscriber(nameof(BackchannelLogoutRequested), Group = "protocol." + nameof(BackchannelLogoutRequested))]
    public async Task OnReceive(Message message)
    {
        var result = await sender.Send(new DeliverCommand(
            new Requested(message.TenantId, message.ClientId, message.Sid, message.SubjectId)));
        if (result.IsFailed)
            throw new BackchannelLogoutDeliveryException(message.TenantId, message.ClientId, message.Sid);

        switch (result.Value)
        {
            case DeliveryOutcome.TransientFailure:
                throw new BackchannelLogoutDeliveryException(message.TenantId, message.ClientId, message.Sid);
            case DeliveryOutcome.Rejected:
                logger.LogWarning(
                    "Client {ClientId} (tenant {TenantId}) rejected the back-channel logout for session {Sid}; not retried.",
                    message.ClientId, message.TenantId, message.Sid);
                break;
            case DeliveryOutcome.Skipped:
                logger.LogDebug(
                    "No back-channel logout for client {ClientId} (tenant {TenantId}): deleted, disabled or no URI.",
                    message.ClientId, message.TenantId);
                break;
            case DeliveryOutcome.Delivered:
                break;
        }
    }
}
