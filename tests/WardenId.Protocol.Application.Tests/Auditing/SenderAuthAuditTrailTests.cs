using Microsoft.Extensions.Logging.Abstractions;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Auditing;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Application.Tests.Auditing;

public class SenderAuthAuditTrailTests
{
    private static readonly RecordAuditEventCommand Event = new(
        SecurityEvent.SessionStarted, Guid.NewGuid(), SessionBuilder.Now, Actor.User(Guid.NewGuid()), []);

    [Fact]
    public async Task RecordAsync_SendsTheEventToTheAuditModule()
    {
        var sender = new StubSender().Returns<RecordAuditEventCommand, Result>(Result.Ok());

        await new SenderAuthAuditTrail(sender, NullLogger<SenderAuthAuditTrail>.Instance).RecordAsync(Event);

        Assert.Equal(Event, Assert.Single(sender.Sent));
    }

    [Fact]
    public async Task RecordAsync_FailedResult_IsSwallowed()
    {
        var sender = new StubSender().Returns<RecordAuditEventCommand, Result>(Result.Fail("not recorded"));

        await new SenderAuthAuditTrail(sender, NullLogger<SenderAuthAuditTrail>.Instance).RecordAsync(Event);
    }

    [Fact]
    public async Task RecordAsync_Exception_IsSwallowed_FailOpen()
    {
        // No response configured: StubSender throws, as MediatR does when no handler is registered.
        var sender = new StubSender();

        await new SenderAuthAuditTrail(sender, NullLogger<SenderAuthAuditTrail>.Instance).RecordAsync(Event);

        Assert.Single(sender.Sent);
    }
}
