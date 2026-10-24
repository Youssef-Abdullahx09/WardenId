using WardenId.Audit.Application.Features.Events.List;
using WardenId.SharedKernel.Query.Contracts;
using ReadAuditChannel = WardenId.Audit.Query.Contracts.Features.AuditEvents.AuditChannel;
using ReadAuditResult = WardenId.Audit.Query.Contracts.Features.AuditEvents.AuditResult;
using SortOption = WardenId.Audit.Query.Contracts.Features.AuditEvents.List.SortOption;
using AuditEventRow = WardenId.Audit.Query.Contracts.Features.AuditEvents.List.Item;
using AuditEventsListQuery = WardenId.Audit.Query.Contracts.Features.AuditEvents.List.Query;

namespace WardenId.Audit.Application.Tests.Features.Events.List;

// Aliased inside the namespace: WardenId.Audit.Query (the Dapper read side) would otherwise shadow the request type.
using Query = WardenId.Audit.Application.Features.Events.List.Query;

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

    private static readonly DateTime OccurredAt = new(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);

    private static AuditEventRow Row(byte[]? payload, Guid? id = null, string? eventType = null, string? actor = null,
        string[]? targets = null, ReadAuditResult? outcome = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Channel = ReadAuditChannel.AdminConfig,
            TenantId = TenantId,
            Sequence = 7,
            OccurredAt = OccurredAt,
            Payload = payload,
            EventType = eventType,
            Actor = actor,
            Targets = targets ?? [],
            Outcome = outcome
        };

    private static FakeAuditEventsQuery QueryReturning(params AuditEventRow[] rows) =>
        new() { ListResponse = Result.Ok(new PaginationListResponse<AuditEventRow>(rows.Length, rows)) };

    private static Query Page(int pageNumber = 1, int pageSize = 25, SortDirection sortDirection = SortDirection.Desc) =>
        new(pageNumber, pageSize, sortDirection);

    [Fact]
    public async Task Handle_SummarizesEachEventFromItsPayload()
    {
        var auditEvent = SuspendEvent();
        var search = QueryReturning(Row(CanonicalAuditEventSerializer.Serialize(auditEvent), auditEvent.Id));

        var result = await new Handler(search).Handle(Page(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.RecordsTotal);
        var item = Assert.Single(result.Value.Data);
        Assert.True(item.PayloadAvailable);
        Assert.Equal(AuditEventType.AdminTenantSuspend.Key, item.EventType);
        Assert.Equal(AuditSeverity.Warning, item.Severity);
        Assert.Equal(ReadAuditResult.Failure, item.Result);
        Assert.Equal(AuditOutcomeReason.PolicyDenied.Key, item.OutcomeReason);
        Assert.Equal(auditEvent.Actor.ToSerializedForm(), item.Actor);
        Assert.Equal([$"{AuditTargetType.Tenant.Key}:tenant-1"], item.Targets);
        Assert.Equal(7, item.Sequence);
        Assert.Equal(ReadAuditChannel.AdminConfig, item.Channel);
        Assert.Equal(new DateTimeOffset(OccurredAt), item.OccurredAt);
        Assert.Equal(TimeSpan.Zero, item.OccurredAt.Offset);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_ListsEventsItCantSummarize(bool corrupt)
    {
        var search = QueryReturning(Row(corrupt ? "{\"not\":\"an event\"}"u8.ToArray() : null));

        var result = await new Handler(search).Handle(Page(), CancellationToken.None);

        var item = Assert.Single(result.Value.Data);
        Assert.False(item.PayloadAvailable);
        Assert.Null(item.EventType);
        Assert.Empty(item.Targets);
        Assert.Equal(ReadAuditChannel.AdminConfig, item.Channel);
    }

    [Fact]
    public async Task Handle_SummarizesAPurgedEventFromItsSearchColumns()
    {
        var search = QueryReturning(Row(null,
            eventType: "admin.tenant.suspend",
            actor: "platform:adminuser:admin-1",
            targets: ["tenant:tenant-1"],
            outcome: ReadAuditResult.Failure));

        var result = await new Handler(search).Handle(Page(), CancellationToken.None);

        var item = Assert.Single(result.Value.Data);
        Assert.False(item.PayloadAvailable);
        Assert.Equal("admin.tenant.suspend", item.EventType);
        Assert.Equal("platform:adminuser:admin-1", item.Actor);
        Assert.Equal(["tenant:tenant-1"], item.Targets);
        Assert.Equal(ReadAuditResult.Failure, item.Result);
        Assert.Null(item.Severity);
        Assert.Null(item.OutcomeReason);
    }

    [Fact]
    public async Task Handle_PassesTheFilterThroughAsIs()
    {
        var search = new FakeAuditEventsQuery();
        var from = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-10-16T00:00:00Z");

        await new Handler(search).Handle(
            Page(1, 25) with
            {
                Channel = ReadAuditChannel.TokenOps,
                TenantId = TenantId,
                From = from,
                To = to,
                EventType = " admin.tenant.suspend ",
                Actor = "admin-1",
                Target = "",
                Outcome = ReadAuditResult.Failure
            },
            CancellationToken.None);

        // Trimming and blank-as-absent are the query layer's job.
        Assert.Equal(
            new AuditEventsListQuery(1, 25, SortDirection.Desc)
            {
                Channel = ReadAuditChannel.TokenOps,
                TenantId = TenantId,
                From = from,
                To = to,
                EventType = " admin.tenant.suspend ",
                Actor = "admin-1",
                Target = "",
                Outcome = ReadAuditResult.Failure,
                SortOption = SortOption.OccurredAt
            },
            Assert.Single(search.ListCalls));
    }

    [Fact]
    public async Task Handle_DefaultsToNewestFirst()
    {
        var search = new FakeAuditEventsQuery();

        await new Handler(search).Handle(Page(), CancellationToken.None);

        var call = Assert.Single(search.ListCalls);
        Assert.Equal(SortOption.OccurredAt, call.SortOption);
        Assert.Equal(SortDirection.Desc, call.SortDirection);
    }

    [Theory]
    [InlineData(SortOption.OccurredAt, SortDirection.Desc)]
    [InlineData(SortOption.OccurredAt, SortDirection.Asc)]
    [InlineData(SortOption.Sequence, SortDirection.Asc)]
    [InlineData(SortOption.Sequence, SortDirection.Desc)]
    public async Task Handle_PassesTheSort(SortOption sortOption, SortDirection sortDirection)
    {
        var search = new FakeAuditEventsQuery();

        await new Handler(search).Handle(Page(sortDirection: sortDirection) with { SortOption = sortOption }, CancellationToken.None);

        var call = Assert.Single(search.ListCalls);
        Assert.Equal(sortOption, call.SortOption);
        Assert.Equal(sortDirection, call.SortDirection);
    }

    [Theory]
    [InlineData((SortOption)0, SortDirection.Asc)]
    [InlineData((SortOption)99, SortDirection.Asc)]
    [InlineData(SortOption.Sequence, (SortDirection)0)]
    [InlineData(SortOption.Sequence, (SortDirection)3)]
    public async Task Handle_FailsValidation_WhenTheSortIsUndefined(SortOption sortOption, SortDirection sortDirection)
    {
        var search = new FakeAuditEventsQuery();

        var result = await new Handler(search).Handle(
            Page(sortDirection: sortDirection) with { SortOption = sortOption },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(AuditErrorCodes.Validation, result.Errors[0].Metadata[AuditErrorCodes.MetadataKey]);
        Assert.Empty(search.ListCalls);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Handle_RejectsBadPaging(int pageNumber, int pageSize)
    {
        var result = await new Handler(new FakeAuditEventsQuery()).Handle(Page(pageNumber, pageSize), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(AuditErrorCodes.Validation, result.Errors[0].Metadata[AuditErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_RejectsAnInvertedTimeRange()
    {
        var result = await new Handler(new FakeAuditEventsQuery()).Handle(
            Page() with { From = DateTimeOffset.Parse("2026-10-16T00:00:00Z"), To = DateTimeOffset.Parse("2026-10-01T00:00:00Z") },
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_PassesAQueryFailureThrough()
    {
        var search = new FakeAuditEventsQuery { ListResponse = Result.Fail<PaginationListResponse<AuditEventRow>>("boom") };

        var result = await new Handler(search).Handle(Page(), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("boom", result.Errors[0].Message);
    }

    [Fact]
    public async Task Handle_DoesNotQueryWhenTheRequestIsInvalid()
    {
        var search = new FakeAuditEventsQuery();

        await new Handler(search).Handle(Page(pageNumber: 0), CancellationToken.None);

        Assert.Empty(search.ListCalls);
    }
}
