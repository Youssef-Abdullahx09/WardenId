using WardenId.SharedKernel.Query.Contracts;
using WardenId.Tenancy.Application.Features.Tenants.List;
using ListItem = WardenId.Tenancy.Query.Contracts.Features.Tenants.List.Item;
using ListQuery = WardenId.Tenancy.Application.Features.Tenants.List.Query;
using ReadStatus = WardenId.Tenancy.Query.Contracts.Features.Tenants.Status;
using SortOption = WardenId.Tenancy.Query.Contracts.Features.Tenants.List.SortOption;

namespace WardenId.Tenancy.Application.Tests.Features.Tenants.List;

/// <summary>
/// The handler's validation, and what it asks the query layer for. The filtering and ordering are SQL
/// (Tenancy.Query's Features/Tenants/List/Query.sql).
/// </summary>
public class HandlerTests
{
    private readonly FakeTenantsQuery _tenants = new();

    private Handler CreateHandler() => new(_tenants);

    private static ListQuery Query(SortDirection sortDirection = SortDirection.Desc) => new(1, 10, sortDirection);

    [Fact]
    public async Task Handle_PassesPagingAndDefaults_ToTheQuery()
    {
        var result = await CreateHandler().Handle(
            new ListQuery(PageNumber: 2, PageSize: 25, SortDirection.Desc),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var call = Assert.Single(_tenants.ListCalls);
        Assert.Equal(2, call.PageNumber);
        Assert.Equal(25, call.PageSize);
        Assert.Null(call.Status);
        Assert.Equal(SortOption.CreatedAt, call.SortOption);
        Assert.Equal(SortDirection.Desc, call.SortDirection);
    }

    [Fact]
    public async Task Handle_PassesTheStatusFilter()
    {
        await CreateHandler().Handle(Query() with { Status = ReadStatus.Suspended }, CancellationToken.None);

        Assert.Equal(ReadStatus.Suspended, Assert.Single(_tenants.ListCalls).Status);
    }

    [Fact]
    public async Task Handle_ReturnsTheQueryLayersPage_AsIs()
    {
        var page = new PaginationListResponse<ListItem>(3,
        [
            new ListItem
            {
                Id = Guid.NewGuid(), Slug = "initech", DisplayName = "Display initech", Status = ReadStatus.Suspended,
                CreatedAt = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)
            }
        ]);
        _tenants.ListResponse = Result.Ok(page);

        var result = await CreateHandler().Handle(Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(page, result.Value);
    }

    [Theory]
    [InlineData(SortOption.CreatedAt, SortDirection.Desc)]
    [InlineData(SortOption.CreatedAt, SortDirection.Asc)]
    [InlineData(SortOption.Slug, SortDirection.Asc)]
    [InlineData(SortOption.Slug, SortDirection.Desc)]
    [InlineData(SortOption.DisplayName, SortDirection.Asc)]
    [InlineData(SortOption.DisplayName, SortDirection.Desc)]
    public async Task Handle_PassesTheSort(SortOption sortOption, SortDirection sortDirection)
    {
        await CreateHandler().Handle(
            Query(sortDirection) with { SortOption = sortOption },
            CancellationToken.None);

        var call = Assert.Single(_tenants.ListCalls);
        Assert.Equal(sortOption, call.SortOption);
        Assert.Equal(sortDirection, call.SortDirection);
    }

    [Theory]
    [InlineData((SortOption)0, SortDirection.Asc)]
    [InlineData((SortOption)99, SortDirection.Asc)]
    [InlineData(SortOption.Slug, (SortDirection)0)]
    [InlineData(SortOption.Slug, (SortDirection)3)]
    public async Task Handle_FailsValidation_WhenTheSortIsUndefined(SortOption sortOption, SortDirection sortDirection)
    {
        var result = await CreateHandler().Handle(
            Query(sortDirection) with { SortOption = sortOption },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.Validation, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
        Assert.Empty(_tenants.ListCalls);
    }

    [Fact]
    public async Task Handle_PassesTheQueryLayersFailureThrough()
    {
        _tenants.ListResponse = Result.Fail(new Error("sql file missing"));

        var result = await CreateHandler().Handle(Query(), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("sql file missing", result.Errors[0].Message);
    }
}
