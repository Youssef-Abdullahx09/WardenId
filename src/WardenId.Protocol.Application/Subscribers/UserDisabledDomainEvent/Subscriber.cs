using MediatR;
using WardenId.Protocol.Domain.Sessions;
using WardenId.SharedKernel.Application.CapPublisher;
using EndForUserCommand = WardenId.Protocol.Application.Features.Sessions.EndForUser.Command;

namespace WardenId.Protocol.Application.Subscribers.UserDisabledDomainEvent;

/// <summary>A disabled user loses every session.</summary>
public sealed class Subscriber(ISender sender) : ICapSubscriber<Message>
{
    // Group is explicit: CAP silently drops one of two subscriptions sharing (topic, group).
    [CapSubscriber(nameof(UserDisabledDomainEvent), Group = SubscriberGroups.Prefix + nameof(UserDisabledDomainEvent))]
    public async Task OnReceive(Message message)
    {
        // Every unended session of the user across tenants: Identity events carry no tenant and user ids are
        // globally unique.
        var result = await sender.Send(new EndForUserCommand(message.AggregateId, EndReason.UserDisabled, null));

        // Throwing makes CAP retry (CapPublisher:RetryCount / RetryInterval).
        if (result.IsFailed)
            throw new InvalidOperationException(
                $"Ending the sessions of user {message.AggregateId} failed: {string.Join("; ", result.Errors.Select(error => error.Message))}");
    }
}
