using Microsoft.Extensions.Time.Testing;
using WardenId.Protocol.Application.Features.Sessions.List;
using WardenId.Protocol.Domain.Sessions;
using WardenId.SharedKernel.Query.Contracts;
using ListQuery = WardenId.Protocol.Application.Features.Sessions.List.Query;
using QueryItem = WardenId.Protocol.Query.Contracts.Features.Sessions.List.Item;
using SessionsQuery = WardenId.Protocol.Query.Contracts.Features.Sessions.List.Query;

namespace WardenId.Protocol.Application.Tests.Features.Sessions.List;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static Task<Result<PaginationListResponse<SessionListItem>>> HandleAsync(
        FakeSessionsQuery sessionsQuery, ListQuery query) =>
        new Handler(sessionsQuery, new FakeTimeProvider(Now)).Handle(query, CancellationToken.None);

    [Fact]
    public async Task PassesFiltersPageAndCurrentTime_ToTheQueryLayer()
    {
        var sessionsQuery = new FakeSessionsQuery();

        await HandleAsync(sessionsQuery, new ListQuery(TenantId, 2, 25, UserId, "web", IncludeEnded: true));

        Assert.Equal(
            new SessionsQuery(TenantId, UserId, "web", IncludeEnded: true, Now, PageNumber: 2, PageSize: 25),
            Assert.Single(sessionsQuery.ListCalls));
    }

    [Fact]
    public async Task MapsRows_InQueryOrder_WithUtcTimesAndEndReason()
    {
        var live = Guid.NewGuid();
        var ended = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var sessionsQuery = new FakeSessionsQuery
        {
            ListResponse = Result.Ok(new PaginationListResponse<QueryItem>(7,
            [
                new QueryItem
                {
                    Sid = live,
                    UserId = UserId,
                    AuthTime = Now.AddHours(-1).UtcDateTime,
                    ExpiresAt = Now.AddHours(23).UtcDateTime,
                    ClientIds = ["spa", "web"]
                },
                new QueryItem
                {
                    Sid = ended,
                    UserId = otherUser,
                    AuthTime = Now.AddHours(-3).UtcDateTime,
                    ExpiresAt = Now.AddHours(21).UtcDateTime,
                    EndedAt = Now.AddHours(-2).UtcDateTime,
                    EndReason = nameof(EndReason.AdminEnded)
                }
            ]))
        };

        var result = await HandleAsync(sessionsQuery, new ListQuery(TenantId, 1, 2, null, null, IncludeEnded: true));

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.RecordsTotal);
        Assert.Collection(
            result.Value.Data,
            s =>
            {
                Assert.Equal(live, s.Sid);
                Assert.Equal(UserId, s.UserId);
                Assert.Equal(Now.AddHours(-1), s.AuthTime);
                Assert.Equal(TimeSpan.Zero, s.AuthTime.Offset);
                Assert.Equal(Now.AddHours(23), s.ExpiresAt);
                Assert.Null(s.EndedAt);
                Assert.Null(s.EndReason);
                Assert.Equal(new[] { "spa", "web" }, s.ClientIds);
            },
            s =>
            {
                Assert.Equal(ended, s.Sid);
                Assert.Equal(otherUser, s.UserId);
                Assert.Equal(Now.AddHours(-2), s.EndedAt);
                Assert.Equal(TimeSpan.Zero, s.EndedAt!.Value.Offset);
                Assert.Equal(EndReason.AdminEnded, s.EndReason);
                Assert.Empty(s.ClientIds);
            });
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, Handler.MaxPageSize + 1)]
    public async Task InvalidPage_FailsValidation_WithoutQuerying(int pageNumber, int pageSize)
    {
        var sessionsQuery = new FakeSessionsQuery();

        var result = await HandleAsync(sessionsQuery, new ListQuery(TenantId, pageNumber, pageSize, null, null, false));

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.Validation, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
        Assert.Empty(sessionsQuery.ListCalls);
    }

    [Fact]
    public async Task QueryFailure_IsPropagated()
    {
        var failure = new Error("File not found !");
        var sessionsQuery = new FakeSessionsQuery { ListResponse = Result.Fail(failure) };

        var result = await HandleAsync(sessionsQuery, new ListQuery(TenantId, 1, 10, null, null, false));

        Assert.True(result.IsFailed);
        Assert.Same(failure, result.Errors[0]);
    }
}
