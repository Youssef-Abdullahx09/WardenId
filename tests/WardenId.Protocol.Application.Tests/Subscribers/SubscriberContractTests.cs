using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCore.CAP;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Domain.Sessions;
using WardenId.SharedKernel.Domain;
using AccountLockedOut = WardenId.Protocol.Application.Subscribers.AccountLockedOutDomainEvent;
using BackchannelLogout = WardenId.Protocol.Application.Subscribers.BackchannelLogoutRequested;
using IdentityEvents = WardenId.Identity.Domain.Events;
using PasswordChanged = WardenId.Protocol.Application.Subscribers.PasswordChangedDomainEvent;
using PasswordReset = WardenId.Protocol.Application.Subscribers.PasswordResetDomainEvent;
using ProtocolEvents = WardenId.Protocol.Domain.Events;
using SessionEnded = WardenId.Protocol.Application.Subscribers.SessionEndedDomainEvent;
using UserDisabled = WardenId.Protocol.Application.Subscribers.UserDisabledDomainEvent;
using UserSoftDeleted = WardenId.Protocol.Application.Subscribers.UserSoftDeletedDomainEvent;

namespace WardenId.Protocol.Application.Tests.Subscribers;

/// <summary>
/// CAP silently drops one of two subscriptions sharing (topic, group), and binds a topic by its string name only.
/// These tests pin the topic names Protocol's subscribers depend on, and the JSON shape crossing the module boundary.
/// </summary>
public class SubscriberContractTests
{
    // As configured in SharedKernel.Infrastructure AddCapService (CAP serializes the runtime type of the message object).
    private static readonly JsonSerializerOptions CapJson = new() { ReferenceHandler = ReferenceHandler.IgnoreCycles };

    private static Dictionary<Type, CapSubscribeAttribute> Subscriptions() =>
        typeof(SessionEnded.Subscriber).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetCustomAttributes<CapSubscribeAttribute>().Select(attribute => (method.DeclaringType!, attribute)))
            .ToDictionary(pair => pair.Item1, pair => pair.attribute);

    private static TMessage RoundTrip<TMessage>(object published) =>
        JsonSerializer.Deserialize<TMessage>(JsonSerializer.Serialize(published, CapJson), CapJson)!;

    [Fact]
    public void EverySubscription_UsesTheProtocolGroup_AndIsUnique()
    {
        var subscriptions = Subscriptions().Values.ToList();

        Assert.Equal(7, subscriptions.Count);
        Assert.All(subscriptions, subscription => Assert.Equal("protocol." + subscription.Name, subscription.Group));
        Assert.Equal(subscriptions.Count, subscriptions.Select(subscription => (subscription.Name, subscription.Group)).Distinct().Count());
    }

    [Fact]
    public void Topics_AreThePublishedEventTypeNames()
    {
        // DomainEventsInterceptor publishes under domainEvent.GetType().Name; the queue publishes BackchannelLogoutRequested.Topic.
        var subscriptions = Subscriptions();

        Assert.Equal(nameof(IdentityEvents.PasswordChangedDomainEvent), subscriptions[typeof(PasswordChanged.Subscriber)].Name);
        Assert.Equal(nameof(IdentityEvents.PasswordResetDomainEvent), subscriptions[typeof(PasswordReset.Subscriber)].Name);
        Assert.Equal(nameof(IdentityEvents.UserDisabledDomainEvent), subscriptions[typeof(UserDisabled.Subscriber)].Name);
        Assert.Equal(nameof(IdentityEvents.UserSoftDeletedDomainEvent), subscriptions[typeof(UserSoftDeleted.Subscriber)].Name);
        Assert.Equal(nameof(ProtocolEvents.SessionEndedDomainEvent), subscriptions[typeof(SessionEnded.Subscriber)].Name);
        Assert.Equal(BackchannelLogoutRequested.Topic, subscriptions[typeof(BackchannelLogout.Subscriber)].Name);
        Assert.Equal(nameof(IdentityEvents.AccountLockedOutDomainEvent), subscriptions[typeof(AccountLockedOut.Subscriber)].Name);
    }

    [Fact]
    public void IdentityEventJson_BindsToEachIdentityMessage()
    {
        var userId = Guid.CreateVersion7();
        DomainEvent published = new IdentityEvents.UserDisabledDomainEvent("abuse");
        published.SetAggregateId(userId);

        Assert.Equal(userId, RoundTrip<PasswordChanged.Message>(published).AggregateId);
        Assert.Equal(userId, RoundTrip<PasswordReset.Message>(published).AggregateId);
        Assert.Equal(userId, RoundTrip<UserDisabled.Message>(published).AggregateId);
        Assert.Equal(userId, RoundTrip<UserSoftDeleted.Message>(published).AggregateId);
    }

    [Fact]
    public void AccountLockedOutDomainEventJson_BindsToAccountLockedOutMessage()
    {
        var userId = Guid.CreateVersion7();
        DomainEvent published = new IdentityEvents.AccountLockedOutDomainEvent(DateTimeOffset.UtcNow.AddMinutes(15));
        published.SetAggregateId(userId);

        Assert.Equal(userId, RoundTrip<AccountLockedOut.Message>(published).AggregateId);
    }

    [Fact]
    public void SessionEndedDomainEventJson_BindsToSessionEndedMessage()
    {
        var published = new ProtocolEvents.SessionEndedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), EndReason.PasswordChanged,
            [new SessionAuthorizationSnapshot("a1", "web", false), new SessionAuthorizationSnapshot("a2", "spa", true)]);

        var received = RoundTrip<SessionEnded.Message>(published);

        Assert.Equal(published.Sid, received.Sid);
        Assert.Equal(published.TenantId, received.TenantId);
        Assert.Equal(published.SubjectId, received.SubjectId);
        Assert.Equal(published.Reason, received.Reason);
        Assert.Equal(published.Authorizations, received.Authorizations);
        // The audit trail's session-end time.
        Assert.Equal(published.OccurredOnUtc, received.OccurredOnUtc);
    }

    [Fact]
    public void BackchannelLogoutRequestedJson_BindsToBackchannelLogoutMessage()
    {
        var published = new BackchannelLogoutRequested(Guid.NewGuid(), "web", Guid.NewGuid(), Guid.NewGuid());

        var received = RoundTrip<BackchannelLogout.Message>(published);

        Assert.Equal(new BackchannelLogout.Message(published.TenantId, published.ClientId, published.Sid, published.SubjectId), received);
    }
}
