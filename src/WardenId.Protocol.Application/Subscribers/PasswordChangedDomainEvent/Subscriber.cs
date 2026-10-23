using MediatR;
using WardenId.Protocol.Domain.Sessions;
using WardenId.SharedKernel.Application.CapPublisher;
using EndForUserCommand = WardenId.Protocol.Application.Features.Sessions.EndForUser.Command;

namespace WardenId.Protocol.Application.Subscribers.PasswordChangedDomainEvent;

/// <summary>A changed password invalidates every session of the user.</summary>
public sealed class Subscriber(ISender sender) : ICapSubscriber<Message>
{
    // Group is explicit: CAP silently drops one of two subscriptions sharing (topic, group).
    [CapSubscriber(nameof(PasswordChangedDomainEvent), Group = SubscriberGroups.Prefix + nameof(PasswordChangedDomainEvent))]
    public async Task OnReceive(Message message)
    {
        // Every unended session of the user across tenants: Identity events carry no tenant and user ids are
        // globally unique.
        var result = await sender.Send(new EndForUserCommand(message.AggregateId, EndReason.PasswordChanged, null));

        // Throwing makes CAP retry (CapPublisher:RetryCount / RetryInterval).
        if (result.IsFailed)
            throw new InvalidOperationException(
                $"Ending the sessions of user {message.AggregateId} failed: {string.Join("; ", result.Errors.Select(error => error.Message))}");
    }
}
