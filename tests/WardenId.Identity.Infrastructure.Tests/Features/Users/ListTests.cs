using WardenId.Identity.Application.Features.Users.List;

namespace WardenId.Identity.Infrastructure.Tests.Features.Users;

/// <summary>
/// The List handler's search and sort, translated to SQL by Npgsql: EF InMemory evaluates
/// StartsWith/ToLower in .NET, so only Postgres shows the LIKE escaping and case handling.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ListTests(PostgresFixture database)
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [SkippableTheory]
    [InlineData("ali", new[] { "alice@example.com" })]
    [InlineData("ALICE.S", new[] { "alice@example.com" })]
    [InlineData("Bob@", new[] { "bob@corp.test" })]
    [InlineData("  bobby ", new[] { "bob@corp.test" })]
    [InlineData("", new[] { "alice@example.com", "bob@corp.test", "carol@example.com" })]
    [InlineData("c_", new[] { "carol@example.com" })]
    [InlineData("%", new string[0])]
    [InlineData("\\", new string[0])]
    [InlineData("example", new string[0])]
    public async Task Search_MatchesEmailOrUsernamePrefix_IgnoringCase_WithLiteralWildcards(string search, string[] expectedEmails)
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        await SeedAsync();

        var result = await ListAsync(search, sort: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedEmails.Length, result.Value.RecordsTotal);
        Assert.Equal(expectedEmails.Order(), result.Value.Data.Select(u => u.Email).Order());
    }

    [SkippableTheory]
    [InlineData(null, new[] { "carol@example.com", "bob@corp.test", "alice@example.com" })]
    [InlineData("createdAt", new[] { "alice@example.com", "bob@corp.test", "carol@example.com" })]
    [InlineData("email", new[] { "alice@example.com", "bob@corp.test", "carol@example.com" })]
    [InlineData("-email", new[] { "carol@example.com", "bob@corp.test", "alice@example.com" })]
    [InlineData("Username", new[] { "alice@example.com", "bob@corp.test", "carol@example.com" })]
    [InlineData("-username", new[] { "carol@example.com", "bob@corp.test", "alice@example.com" })]
    public async Task Sort_OrdersTheResults(string? sort, string[] expectedEmails)
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        await SeedAsync();

        var result = await ListAsync(search: null, sort);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedEmails, result.Value.Data.Select(u => u.Email));
    }

    [SkippableFact]
    public async Task SearchAndSort_ApplyWithTheStatusFilterAndPaging()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        await SeedAsync();

        await using var context = database.CreateContext();
        var result = await new Handler(context).Handle(
            new Query(_tenantId, PageNumber: 2, PageSize: 1, UserStatus.PendingVerification, Search: "a", Sort: "-email"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.RecordsTotal);
        Assert.Empty(result.Value.Data);
    }

    private async Task<FluentResults.Result<SharedKernel.Query.Contracts.PaginationListResponse<UserListItem>>> ListAsync(string? search, string? sort)
    {
        await using var context = database.CreateContext();
        return await new Handler(context).Handle(new Query(_tenantId, 1, 10, null, search, sort), CancellationToken.None);
    }

    /// <summary>Alice, then Bob, then Carol, in separate saves so their CreatedAt values differ.</summary>
    private async Task SeedAsync()
    {
        foreach (var (email, username) in new[] { ("alice@example.com", "Alice.Smith"), ("bob@corp.test", "bobby"), ("carol@example.com", "c_ross") })
        {
            await using var context = database.CreateContext();
            var user = User.Register(_tenantId, Email.Create(email).Value, Username.Create(username).Value).Value;
            await new UserRepository(context).CreateAsync(user, Guid.NewGuid());
            await Task.Delay(5);
        }
    }
}
