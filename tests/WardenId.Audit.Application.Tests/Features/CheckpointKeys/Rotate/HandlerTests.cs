using WardenId.Audit.Application.Features.CheckpointKeys.Rotate;

namespace WardenId.Audit.Application.Tests.Features.CheckpointKeys.Rotate;

public class HandlerTests
{
    private sealed class FakeRotator(string? previousKid, string newKid) : IAuditCheckpointKeyRotator
    {
        public AuditEvent? Recorded { get; private set; }
        public int Calls { get; private set; }

        public Task<Result<CheckpointKeyRotationResult>> RotateAsync(
            Func<string?, string, AuditEvent> buildAuditEvent, CancellationToken cancellationToken = default)
        {
            Calls++;
            Recorded = buildAuditEvent(previousKid, newKid);
            return Task.FromResult(Result.Ok(new CheckpointKeyRotationResult(newKid, previousKid)));
        }
    }

    private static readonly Actor Admin = Actor.Create(ActorType.AuthenticatedUnknown, "admin-7", ActorScope.ForTenant(Guid.NewGuid())).Value;

    [Fact]
    public async Task Handle_ReturnsTheNewKid_AndRecordsTheRotationAsThePlatformAdminEvent()
    {
        var rotator = new FakeRotator("old-kid", "new-kid");
        var accessor = new FakeAuditContextAccessor();
        using var scope = accessor.BeginScope(new AuditAmbientContext(Admin, Guid.NewGuid(), Correlation.Create(requestId: "r-1"), ClientContext.Unknown()));

        var result = await new Handler(rotator, accessor).Handle(new Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-kid", result.Value.Kid);
        var recorded = rotator.Recorded!;
        Assert.Equal(AuditEventType.AuditCheckpointKeyRotate, recorded.EventType);
        Assert.Equal(AuditChannel.Platform, recorded.Channel);
        Assert.Equal(Admin, recorded.Actor);
        Assert.Equal("checkpoint_signing_key:new-kid", recorded.Targets.Single().ToSerializedForm());
        var change = recorded.Changes.Single();
        Assert.Equal("ActiveKid", change.PropertyName);
        Assert.Equal("old-kid", ((AuditValue.Plain)change.OldValue!).DisplayValue);
        Assert.Equal("new-kid", ((AuditValue.Plain)change.NewValue!).DisplayValue);
    }

    [Fact]
    public async Task Handle_FirstKey_HasNoOldValue()
    {
        var rotator = new FakeRotator(null, "first-kid");
        var accessor = new FakeAuditContextAccessor();
        using var scope = accessor.BeginScope(new AuditAmbientContext(Admin, null, Correlation.None(), ClientContext.Unknown()));

        await new Handler(rotator, accessor).Handle(new Command(), CancellationToken.None);

        Assert.Null(rotator.Recorded!.Changes.Single().OldValue);
    }

    [Fact]
    public async Task Handle_WithoutAmbientAuditContext_FailsWithoutRotating()
    {
        var rotator = new FakeRotator("old-kid", "new-kid");

        var result = await new Handler(rotator, new FakeAuditContextAccessor()).Handle(new Command(), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(0, rotator.Calls);
    }
}
