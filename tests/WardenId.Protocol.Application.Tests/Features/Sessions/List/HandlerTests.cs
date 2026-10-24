using Microsoft.Extensions.Time.Testing;
using WardenId.Protocol.Application.Features.Sessions.List;
using WardenId.SharedKernel.Query.Contracts;
using ListItem = WardenId.Protocol.Query.Contracts.Features.Sessions.List.Item;
using ListQuery = WardenId.Protocol.Application.Features.Sessions.List.Query;
using ReadEndReason = WardenId.Protocol.Query.Contracts.Features.Sessions.EndReason;
using SortOption = WardenId.Protocol.Query.Contracts.Features.Sessions.List.SortOption;

namespace WardenId.Protocol.Application.Tests.Features.Sessions.List;

/// <summary>
/// The handler's validation, and what it asks the query layer for. The filtering, ordering and paging are SQL
/// (Protocol.Query's Features/Sessions/List/Query.sql).
/// </summary>
public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeSessionsQuery _sessions = new();

    private Task<Result<PaginationListResponse<ListItem>>> HandleAsync(ListQuery query) =>
        new Handler(_sessions, new FakeTimeProvider(Now)).Handle(query, CancellationToken.None);

    private static ListQuery Query(SortDirection sortDirection = SortDirection.Desc) => new(TenantId, 1, 10, sortDirection);

    [Fact]
    public async Task PassesTenantPagingAndDefaults_ToTheQueryLayer()
    {
        var result = await HandleAsync(new ListQuery(TenantId, PageNumber: 2, PageSize: 25, SortDirection.Desc));

        Assert.True(result.IsSuccess);
        var call = Assert.Single(_sessions.ListCalls);
        Assert.Equal(TenantId, call.TenantId);
        Assert.Equal(2, call.PageNumber);
        Assert.Equal(25, call.PageSize);
        Assert.Null(call.UserId);
        Assert.Null(call.ClientId);
        Assert.False(call.IncludeEnded);
        Assert.Equal(Now, call.Now);
        Assert.Equal(SortOption.AuthTime, call.SortOption);
        Assert.Equal(SortDirection.Desc, call.SortDirection);
    }

    [Fact]
    public async Task PassesFiltersAndCurrentTime_ToTheQueryLayer()
    {
        await HandleAsync(Query() with { UserId = UserId, ClientId = "web", IncludeEnded = true });

        var call = Assert.Single(_sessions.ListCalls);
        Assert.Equal(UserId, call.UserId);
        Assert.Equal("web", call.ClientId);
        Assert.True(call.IncludeEnded);
        Assert.Equal(Now, call.Now);
    }

    [Fact]
    public async Task ReturnsTheQueryLayersPage_AsIs()
    {
        var page = new PaginationListResponse<ListItem>(7,
        [
            new ListItem
            {
                Sid = Guid.NewGuid(),
                UserId = UserId,
                AuthTime = Now.AddHours(-3).UtcDateTime,
                ExpiresAt = Now.AddHours(21).UtcDateTime,
                EndedAt = Now.AddHours(-2).UtcDateTime,
                EndReason = ReadEndReason.AdminEnded,
                ClientIds = ["spa", "web"]
            }
        ]);
        _sessions.ListResponse = Result.Ok(page);

        var result = await HandleAsync(Query());

        Assert.True(result.IsSuccess);
        Assert.Same(page, result.Value);
    }

    [Theory]
    [InlineData(SortOption.AuthTime, SortDirection.Desc)]
    [InlineData(SortOption.AuthTime, SortDirection.Asc)]
    [InlineData(SortOption.ExpiresAt, SortDirection.Asc)]
    [InlineData(SortOption.ExpiresAt, SortDirection.Desc)]
    public async Task PassesTheSort(SortOption sortOption, SortDirection sortDirection)
    {
        await HandleAsync(Query(sortDirection) with { SortOption = sortOption });

        var call = Assert.Single(_sessions.ListCalls);
        Assert.Equal(sortOption, call.SortOption);
        Assert.Equal(sortDirection, call.SortDirection);
    }

    [Theory]
    [InlineData((SortOption)0, SortDirection.Asc)]
    [InlineData((SortOption)99, SortDirection.Asc)]
    [InlineData(SortOption.ExpiresAt, (SortDirection)0)]
    [InlineData(SortOption.ExpiresAt, (SortDirection)3)]
    public async Task UndefinedSort_FailsValidation_WithoutQuerying(SortOption sortOption, SortDirection sortDirection)
    {
        var result = await HandleAsync(Query(sortDirection) with { SortOption = sortOption });

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.Validation, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
        Assert.Empty(_sessions.ListCalls);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, Handler.MaxPageSize + 1)]
    public async Task InvalidPage_FailsValidation_WithoutQuerying(int pageNumber, int pageSize)
    {
        var result = await HandleAsync(new ListQuery(TenantId, pageNumber, pageSize, SortDirection.Desc));

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.Validation, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
        Assert.Empty(_sessions.ListCalls);
    }

    [Fact]
    public async Task QueryFailure_IsPropagated()
    {
        var failure = new Error("File not found !");
        _sessions.ListResponse = Result.Fail(failure);

        var result = await HandleAsync(Query());

        Assert.True(result.IsFailed);
        Assert.Same(failure, result.Errors[0]);
    }
}
