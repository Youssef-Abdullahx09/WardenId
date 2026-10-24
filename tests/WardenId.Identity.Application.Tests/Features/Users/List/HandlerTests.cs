using WardenId.Identity.Application.Features.Users.List;
using WardenId.SharedKernel.Query.Contracts;
using ListItem = WardenId.Identity.Query.Contracts.Features.Users.List.Item;
using ListQuery = WardenId.Identity.Application.Features.Users.List.Query;
using UserStatus = WardenId.Identity.Query.Contracts.Features.Users.UserStatus;
using SortOption = WardenId.Identity.Query.Contracts.Features.Users.List.SortOption;

namespace WardenId.Identity.Application.Tests.Features.Users.List;

/// <summary>
/// The handler's validation, and what it asks the query layer for. The filtering, search and ordering are SQL
/// (Identity.Query's Features/Users/List/Query.sql), covered on Postgres by WardenId.Identity.Infrastructure.Tests.
/// </summary>
public class HandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeUsersQuery _users = new();

    private Handler CreateHandler() => new(_users);

    private ListQuery Query(SortDirection sortDirection = SortDirection.Desc) => new(_tenantId, 1, 10, sortDirection);

    [Fact]
    public async Task Handle_PassesTenantPagingAndDefaults_ToTheQuery()
    {
        var result = await CreateHandler().Handle(new ListQuery(_tenantId, PageNumber: 2, PageSize: 25, SortDirection.Desc), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var call = Assert.Single(_users.ListCalls);
        Assert.Equal(_tenantId, call.TenantId);
        Assert.Equal(2, call.PageNumber);
        Assert.Equal(25, call.PageSize);
        Assert.False(call.IncludeDeleted);
        Assert.Null(call.Status);
        Assert.Null(call.Search);
        Assert.Equal(SortOption.CreatedAt, call.SortOption);
        Assert.Equal(SortDirection.Desc, call.SortDirection);
    }

    [Fact]
    public async Task Handle_ReturnsTheQueryLayersPage_AsIs()
    {
        var page = new PaginationListResponse<ListItem>(7,
        [
            new ListItem { Id = Guid.NewGuid(), Email = "a@example.com", Username = "Alice", Status = UserStatus.Active }
        ]);
        _users.ListResponse = Result.Ok(page);

        var result = await CreateHandler().Handle(Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(page, result.Value);
    }

    [Theory]
    [InlineData(UserStatus.Active, false)]
    [InlineData(UserStatus.SoftDeleted, true)]
    public async Task Handle_FiltersByStatus_AndIncludesDeletedOnlyForSoftDeleted(UserStatus status, bool includeDeleted)
    {
        await CreateHandler().Handle(Query() with { Status = status }, CancellationToken.None);

        var call = Assert.Single(_users.ListCalls);
        Assert.Equal(status, call.Status);
        Assert.Equal(includeDeleted, call.IncludeDeleted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ALICE.S ")]
    [InlineData("c_%")]
    public async Task Handle_PassesTheSearchThrough(string? search)
    {
        await CreateHandler().Handle(Query() with { Search = search }, CancellationToken.None);

        Assert.Equal(search, Assert.Single(_users.ListCalls).Search);
    }

    [Fact]
    public async Task Handle_FailsValidation_WhenSearchIsLongerThanAnEmail()
    {
        var result = await CreateHandler().Handle(
            Query() with { Search = new string('a', Email.MaxLength + 1) },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Empty(_users.ListCalls);
    }

    [Theory]
    [InlineData(SortOption.CreatedAt, SortDirection.Desc)]
    [InlineData(SortOption.CreatedAt, SortDirection.Asc)]
    [InlineData(SortOption.Email, SortDirection.Asc)]
    [InlineData(SortOption.Username, SortDirection.Desc)]
    [InlineData(SortOption.FirstName, SortDirection.Asc)]
    [InlineData(SortOption.LastName, SortDirection.Desc)]
    public async Task Handle_PassesTheSort(SortOption sortOption, SortDirection sortDirection)
    {
        await CreateHandler().Handle(
            Query(sortDirection) with { SortOption = sortOption },
            CancellationToken.None);

        var call = Assert.Single(_users.ListCalls);
        Assert.Equal(sortOption, call.SortOption);
        Assert.Equal(sortDirection, call.SortDirection);
    }

    [Theory]
    [InlineData((SortOption)0, SortDirection.Asc)]
    [InlineData((SortOption)99, SortDirection.Asc)]
    [InlineData(SortOption.Email, (SortDirection)0)]
    [InlineData(SortOption.Email, (SortDirection)3)]
    public async Task Handle_FailsValidation_WhenTheSortIsUndefined(SortOption sortOption, SortDirection sortDirection)
    {
        var result = await CreateHandler().Handle(
            Query(sortDirection) with { SortOption = sortOption },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Empty(_users.ListCalls);
    }

    [Fact]
    public async Task Handle_PassesTheQueryLayersFailureThrough()
    {
        _users.ListResponse = Result.Fail(new Error("sql file missing"));

        var result = await CreateHandler().Handle(Query(), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("sql file missing", result.Errors[0].Message);
    }
}
