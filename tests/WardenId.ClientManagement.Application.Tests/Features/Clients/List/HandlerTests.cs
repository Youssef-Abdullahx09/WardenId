using WardenId.ClientManagement.Application.Features.Clients.List;
using WardenId.SharedKernel.Query.Contracts;
using ListItem = WardenId.ClientManagement.Query.Contracts.Features.Clients.List.Item;
using ListQuery = WardenId.ClientManagement.Application.Features.Clients.List.Query;
using ReadClientType = WardenId.ClientManagement.Query.Contracts.Features.Clients.ClientType;
using SortOption = WardenId.ClientManagement.Query.Contracts.Features.Clients.List.SortOption;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.List;

/// <summary>
/// The handler's validation, and what it asks the query layer for. The filtering and ordering are SQL
/// (ClientManagement.Query's Features/Clients/List/Query.sql).
/// </summary>
public class HandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeClientsQuery _clients = new();

    private Handler CreateHandler() => new(_clients);

    private ListQuery Query(SortDirection sortDirection = SortDirection.Desc) => new(_tenantId, 1, 10, sortDirection);

    [Fact]
    public async Task Handle_PassesTenantPagingAndDefaults_ToTheQuery()
    {
        var result = await CreateHandler().Handle(
            new ListQuery(_tenantId, PageNumber: 2, PageSize: 25, SortDirection.Desc), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var call = Assert.Single(_clients.ListCalls);
        Assert.Equal(_tenantId, call.TenantId);
        Assert.Equal(2, call.PageNumber);
        Assert.Equal(25, call.PageSize);
        Assert.Null(call.Enabled);
        Assert.Null(call.ClientType);
        Assert.Equal(SortOption.CreatedAt, call.SortOption);
        Assert.Equal(SortDirection.Desc, call.SortDirection);
    }

    [Theory]
    [InlineData(true, ReadClientType.Public)]
    [InlineData(false, ReadClientType.Confidential)]
    public async Task Handle_PassesTheFiltersThrough(bool enabled, ReadClientType clientType)
    {
        await CreateHandler().Handle(
            Query() with { Enabled = enabled, ClientType = clientType },
            CancellationToken.None);

        var call = Assert.Single(_clients.ListCalls);
        Assert.Equal(enabled, call.Enabled);
        Assert.Equal(clientType, call.ClientType);
    }

    [Fact]
    public async Task Handle_ReturnsTheQueryLayersPage_AsIs()
    {
        var page = new PaginationListResponse<ListItem>(7,
        [
            new ListItem
            {
                Id = Guid.NewGuid(), ClientId = "a", DisplayName = "A", ClientType = ReadClientType.Confidential,
                Enabled = true, IsFirstParty = true, CreatedAt = new DateTime(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc)
            }
        ]);
        _clients.ListResponse = Result.Ok(page);

        var result = await CreateHandler().Handle(Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(page, result.Value);
    }

    [Theory]
    [InlineData(SortOption.CreatedAt, SortDirection.Desc)]
    [InlineData(SortOption.CreatedAt, SortDirection.Asc)]
    [InlineData(SortOption.ClientId, SortDirection.Asc)]
    [InlineData(SortOption.ClientId, SortDirection.Desc)]
    [InlineData(SortOption.DisplayName, SortDirection.Asc)]
    [InlineData(SortOption.DisplayName, SortDirection.Desc)]
    public async Task Handle_PassesTheSort(SortOption sortOption, SortDirection sortDirection)
    {
        await CreateHandler().Handle(
            Query(sortDirection) with { SortOption = sortOption },
            CancellationToken.None);

        var call = Assert.Single(_clients.ListCalls);
        Assert.Equal(sortOption, call.SortOption);
        Assert.Equal(sortDirection, call.SortDirection);
    }

    [Theory]
    [InlineData((SortOption)0, SortDirection.Asc)]
    [InlineData((SortOption)99, SortDirection.Asc)]
    [InlineData(SortOption.ClientId, (SortDirection)0)]
    [InlineData(SortOption.ClientId, (SortDirection)3)]
    public async Task Handle_FailsValidation_WhenTheSortIsUndefined(SortOption sortOption, SortDirection sortDirection)
    {
        var result = await CreateHandler().Handle(
            Query(sortDirection) with { SortOption = sortOption },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrorCodes.Validation, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);
        Assert.Empty(_clients.ListCalls);
    }

    [Fact]
    public async Task Handle_PassesTheQueryLayersFailureThrough()
    {
        _clients.ListResponse = Result.Fail(new Error("sql file missing"));

        var result = await CreateHandler().Handle(Query(), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("sql file missing", result.Errors[0].Message);
    }
}
