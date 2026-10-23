using WardenId.Identity.Application.Features.Users.List;
using WardenId.SharedKernel.Query.Contracts;
using ListItem = WardenId.Identity.Query.Contracts.Features.Users.List.Item;
using ListQuery = WardenId.Identity.Application.Features.Users.List.Query;
using SortField = WardenId.Identity.Query.Contracts.Features.Users.List.SortField;

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

    [Fact]
    public async Task Handle_PassesTenantPagingAndDefaults_ToTheQuery()
    {
        var result = await CreateHandler().Handle(new ListQuery(_tenantId, PageNumber: 2, PageSize: 25, Status: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var call = Assert.Single(_users.ListCalls);
        Assert.Equal(_tenantId, call.TenantId);
        Assert.Equal(2, call.PageNumber);
        Assert.Equal(25, call.PageSize);
        Assert.False(call.IncludeDeleted);
        Assert.Null(call.Status);
        Assert.Null(call.SearchPrefix);
        Assert.Equal(SortField.CreatedAt, call.SortField);
        Assert.Equal(SortDirection.Desc, call.SortDirection);
    }

    [Fact]
    public async Task Handle_MapsTheQueryRows_AndTotal()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc);
        _users.ListResponse = Result.Ok(new PaginationListResponse<ListItem>(7,
        [
            new ListItem { Id = id, Email = "a@example.com", Username = "Alice", Status = nameof(UserStatus.Active), CreatedAt = createdAt }
        ]));

        var result = await CreateHandler().Handle(new ListQuery(_tenantId, 1, 10, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.RecordsTotal);
        Assert.Equal(
            new UserListItem(id, "a@example.com", "Alice", null, null, UserStatus.Active, new DateTimeOffset(createdAt)),
            Assert.Single(result.Value.Data));
    }

    [Theory]
    [InlineData(UserStatus.Active, false)]
    [InlineData(UserStatus.SoftDeleted, true)]
    public async Task Handle_FiltersByStatus_AndIncludesDeletedOnlyForSoftDeleted(UserStatus status, bool includeDeleted)
    {
        await CreateHandler().Handle(new ListQuery(_tenantId, 1, 10, status), CancellationToken.None);

        var call = Assert.Single(_users.ListCalls);
        Assert.Equal(status.ToString(), call.Status);
        Assert.Equal(includeDeleted, call.IncludeDeleted);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  ALICE.S ", "alice.s")]
    [InlineData("c_%", "c_%")]
    public async Task Handle_TrimsAndLowersTheSearch(string? search, string? expectedPrefix)
    {
        await CreateHandler().Handle(new ListQuery(_tenantId, 1, 10, null, Search: search), CancellationToken.None);

        Assert.Equal(expectedPrefix, Assert.Single(_users.ListCalls).SearchPrefix);
    }

    [Fact]
    public async Task Handle_FailsValidation_WhenSearchIsLongerThanAnEmail()
    {
        var result = await CreateHandler().Handle(
            new ListQuery(_tenantId, 1, 10, null, Search: new string('a', Email.MaxLength + 1)),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Empty(_users.ListCalls);
    }

    [Theory]
    [InlineData(null, SortField.CreatedAt, SortDirection.Desc)]
    [InlineData("-createdAt", SortField.CreatedAt, SortDirection.Desc)]
    [InlineData("createdAt", SortField.CreatedAt, SortDirection.Asc)]
    [InlineData("CREATEDAT", SortField.CreatedAt, SortDirection.Asc)]
    [InlineData("email", SortField.Email, SortDirection.Asc)]
    [InlineData("-email", SortField.Email, SortDirection.Desc)]
    [InlineData("Username", SortField.Username, SortDirection.Asc)]
    [InlineData("-username", SortField.Username, SortDirection.Desc)]
    [InlineData("firstName", SortField.FirstName, SortDirection.Asc)]
    [InlineData("-LASTNAME", SortField.LastName, SortDirection.Desc)]
    public async Task Handle_PassesTheSort(string? sort, SortField field, SortDirection direction)
    {
        await CreateHandler().Handle(new ListQuery(_tenantId, 1, 10, null, Sort: sort), CancellationToken.None);

        var call = Assert.Single(_users.ListCalls);
        Assert.Equal(field, call.SortField);
        Assert.Equal(direction, call.SortDirection);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("-")]
    [InlineData("--email")]
    [InlineData("email desc")]
    [InlineData("2")]
    public async Task Handle_FailsValidation_WhenSortIsUnknown(string sort)
    {
        var result = await CreateHandler().Handle(new ListQuery(_tenantId, 1, 10, null, Sort: sort), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_PassesTheQueryLayersFailureThrough()
    {
        _users.ListResponse = Result.Fail(new Error("sql file missing"));

        var result = await CreateHandler().Handle(new ListQuery(_tenantId, 1, 10, null), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("sql file missing", result.Errors[0].Message);
    }
}
