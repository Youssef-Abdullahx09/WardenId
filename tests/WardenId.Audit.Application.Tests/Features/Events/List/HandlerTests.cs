using WardenId.Audit.Application.Features.Events.List;

namespace WardenId.Audit.Application.Tests.Features.Events.List;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static AuditEvent SuspendEvent() =>
        AuditEvent.Create(
            Guid.NewGuid(),
            AuditChannel.AdminConfig,
            AuditEventType.AdminTenantSuspend,
            1,
            DateTimeOffset.Parse("2026-10-16T08:00:00Z"),
            AuditSeverity.Warning,
            Actor.Create(ActorType.AdminUser, "admin-1", ActorScope.Platform()).Value,
            AuditOutcome.Failure(AuditOutcomeReason.PolicyDenied),
            ClientContext.Unknown(),
            AuthContext.None(),
            Correlation.None(),
            [AuditTarget.Create(AuditTargetType.Tenant, "tenant-1", null).Value],
            []).Value;

    private static AuditEventSearchRow Row(byte[]? payload, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), AuditChannel.AdminConfig, TenantId, 7, DateTimeOffset.Parse("2026-10-16T08:00:00Z"), payload);

    private static Query Page(int pageNumber = 1, int pageSize = 25) => new(pageNumber, pageSize, null, null, null, null);

    [Fact]
    public async Task Handle_SummarizesEachEventFromItsPayload()
    {
        var auditEvent = SuspendEvent();
        var search = new FakeAuditEventSearch();
        search.Rows.Add(Row(CanonicalAuditEventSerializer.Serialize(auditEvent), auditEvent.Id));

        var result = await new Handler(search).Handle(Page(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.RecordsTotal);
        var item = Assert.Single(result.Value.Data);
        Assert.True(item.PayloadAvailable);
        Assert.Equal(AuditEventType.AdminTenantSuspend.Key, item.EventType);
        Assert.Equal(AuditSeverity.Warning, item.Severity);
        Assert.Equal(AuditResult.Failure, item.Result);
        Assert.Equal(AuditOutcomeReason.PolicyDenied.Key, item.OutcomeReason);
        Assert.Equal(auditEvent.Actor.ToSerializedForm(), item.Actor);
        Assert.Equal([$"{AuditTargetType.Tenant.Key}:tenant-1"], item.Targets);
        Assert.Equal(7, item.Sequence);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_ListsEventsItCantSummarize(bool corrupt)
    {
        var search = new FakeAuditEventSearch();
        search.Rows.Add(Row(corrupt ? "{\"not\":\"an event\"}"u8.ToArray() : null));

        var result = await new Handler(search).Handle(Page(), CancellationToken.None);

        var item = Assert.Single(result.Value.Data);
        Assert.False(item.PayloadAvailable);
        Assert.Null(item.EventType);
        Assert.Empty(item.Targets);
        Assert.Equal(AuditChannel.AdminConfig, item.Channel);
    }

    [Fact]
    public async Task Handle_PassesTheFilterOn()
    {
        var search = new FakeAuditEventSearch();
        var from = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-10-16T00:00:00Z");

        await new Handler(search).Handle(new Query(1, 25, AuditChannel.TokenOps, TenantId, from, to), CancellationToken.None);

        Assert.Equal(new AuditEventFilter(AuditChannel.TokenOps, TenantId, from, to), search.LastFilter);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Handle_RejectsBadPaging(int pageNumber, int pageSize)
    {
        var result = await new Handler(new FakeAuditEventSearch()).Handle(Page(pageNumber, pageSize), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(AuditErrorCodes.Validation, result.Errors[0].Metadata[AuditErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_RejectsAnInvertedTimeRange()
    {
        var result = await new Handler(new FakeAuditEventSearch()).Handle(
            new Query(1, 25, null, null, DateTimeOffset.Parse("2026-10-16T00:00:00Z"), DateTimeOffset.Parse("2026-10-01T00:00:00Z")),
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
