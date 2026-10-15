using Microsoft.Extensions.Logging.Abstractions;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Domain.Sessions;
using BackchannelLogout = WardenId.Protocol.Application.Subscribers.BackchannelLogoutRequested;
using DeliverCommand = WardenId.Protocol.Application.Features.BackchannelLogout.Deliver.Command;
using EndForUserCommand = WardenId.Protocol.Application.Features.Sessions.EndForUser.Command;
using PasswordChanged = WardenId.Protocol.Application.Subscribers.PasswordChangedDomainEvent;
using PasswordReset = WardenId.Protocol.Application.Subscribers.PasswordResetDomainEvent;
using UserDisabled = WardenId.Protocol.Application.Subscribers.UserDisabledDomainEvent;
using UserSoftDeleted = WardenId.Protocol.Application.Subscribers.UserSoftDeletedDomainEvent;

namespace WardenId.Protocol.Application.Tests.Subscribers;

public class SubscriberTests
{
    private static readonly BackchannelLogout.Message Request = new(Guid.NewGuid(), "web", Guid.NewGuid(), Guid.NewGuid());

    private static StubSender EndForUserSender() =>
        new StubSender().Returns<EndForUserCommand, Result>(Result.Ok());

    [Theory]
    [InlineData(DeliveryOutcome.Delivered)]
    [InlineData(DeliveryOutcome.Skipped)]
    [InlineData(DeliveryOutcome.Rejected)]
    public async Task BackchannelLogout_FinalOutcomes_AreAcknowledged(DeliveryOutcome outcome)
    {
        var sender = new StubSender().Returns<DeliverCommand, Result<DeliveryOutcome>>(Result.Ok(outcome));

        await new BackchannelLogout.Subscriber(sender, NullLogger<BackchannelLogout.Subscriber>.Instance).OnReceive(Request);

        Assert.Equal(
            new DeliverCommand(new BackchannelLogoutRequested(Request.TenantId, Request.ClientId, Request.Sid, Request.SubjectId)),
            Assert.Single(sender.Sent));
    }

    [Fact]
    public async Task BackchannelLogout_TransientFailure_Throws_SoCapRetries()
    {
        var sender = new StubSender()
            .Returns<DeliverCommand, Result<DeliveryOutcome>>(Result.Ok(DeliveryOutcome.TransientFailure));

        await Assert.ThrowsAsync<BackchannelLogout.BackchannelLogoutDeliveryException>(() =>
            new BackchannelLogout.Subscriber(sender, NullLogger<BackchannelLogout.Subscriber>.Instance).OnReceive(Request));
    }

    [Fact]
    public async Task BackchannelLogout_DeliverFailure_Throws_SoCapRetries()
    {
        var sender = new StubSender().Returns<DeliverCommand, Result<DeliveryOutcome>>(Result.Fail("boom"));

        await Assert.ThrowsAsync<BackchannelLogout.BackchannelLogoutDeliveryException>(() =>
            new BackchannelLogout.Subscriber(sender, NullLogger<BackchannelLogout.Subscriber>.Instance).OnReceive(Request));
    }

    [Fact]
    public async Task PasswordChanged_EndsAllUserSessions_AsPasswordChanged()
    {
        var sender = EndForUserSender();
        var userId = Guid.NewGuid();

        await new PasswordChanged.Subscriber(sender).OnReceive(new PasswordChanged.Message(userId));

        Assert.Equal(new EndForUserCommand(userId, EndReason.PasswordChanged, null), Assert.Single(sender.Sent));
    }

    [Fact]
    public async Task PasswordReset_EndsAllUserSessions_AsPasswordChanged()
    {
        var sender = EndForUserSender();
        var userId = Guid.NewGuid();

        await new PasswordReset.Subscriber(sender).OnReceive(new PasswordReset.Message(userId));

        Assert.Equal(new EndForUserCommand(userId, EndReason.PasswordChanged, null), Assert.Single(sender.Sent));
    }

    [Fact]
    public async Task UserDisabled_EndsAllUserSessions_AsUserDisabled()
    {
        var sender = EndForUserSender();
        var userId = Guid.NewGuid();

        await new UserDisabled.Subscriber(sender).OnReceive(new UserDisabled.Message(userId));

        Assert.Equal(new EndForUserCommand(userId, EndReason.UserDisabled, null), Assert.Single(sender.Sent));
    }

    [Fact]
    public async Task UserSoftDeleted_EndsAllUserSessions_AsUserDeleted()
    {
        var sender = EndForUserSender();
        var userId = Guid.NewGuid();

        await new UserSoftDeleted.Subscriber(sender).OnReceive(new UserSoftDeleted.Message(userId));

        Assert.Equal(new EndForUserCommand(userId, EndReason.UserDeleted, null), Assert.Single(sender.Sent));
    }

    [Fact]
    public async Task IdentitySubscriber_Throws_WhenEndForUserFails_SoCapRetries()
    {
        var sender = new StubSender().Returns<EndForUserCommand, Result>(Result.Fail("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UserDisabled.Subscriber(sender).OnReceive(new UserDisabled.Message(Guid.NewGuid())));
    }
}
