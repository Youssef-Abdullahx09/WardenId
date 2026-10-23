using WardenId.Identity.Application.Features.Users.List;
using ListQuery = WardenId.Identity.Application.Features.Users.List.Query;

namespace WardenId.Identity.Infrastructure.Tests.Features.Users;

/// <summary>
/// The List handler over Identity.Query's Features/Users/List/Query.sql on Postgres: the search's LIKE escaping and
/// case handling, the sort, the status filter and paging.
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
    [InlineData("smi", new[] { "alice@example.com" })]
    [InlineData("ALICE SM", new[] { "alice@example.com" })]
    [InlineData("ackroyd", new[] { "bob@corp.test" })]
    [InlineData("alice smith ", new[] { "alice@example.com" })]
    public async Task Search_MatchesEmailUsernameOrNamePrefix_IgnoringCase_WithLiteralWildcards(string search, string[] expectedEmails)
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
    [InlineData("firstName", new[] { "alice@example.com", "carol@example.com", "bob@corp.test" })]
    [InlineData("-firstName", new[] { "carol@example.com", "alice@example.com", "bob@corp.test" })]
    [InlineData("lastName", new[] { "bob@corp.test", "alice@example.com", "carol@example.com" })]
    [InlineData("-lastName", new[] { "alice@example.com", "bob@corp.test", "carol@example.com" })]
    public async Task Sort_OrdersTheResults_WithoutTheSortedNameLast(string? sort, string[] expectedEmails)
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

        var result = await new Handler(database.CreateUsersQuery()).Handle(
            new ListQuery(_tenantId, PageNumber: 2, PageSize: 1, UserStatus.PendingVerification, Search: "a", Sort: "-email"),
            CancellationToken.None);

        // "a" matches Alice by her email and Bob by his last name (Ackroyd); -email puts Bob on page 1.
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.RecordsTotal);
        Assert.Equal("alice@example.com", Assert.Single(result.Value.Data).Email);
    }

    private async Task<FluentResults.Result<SharedKernel.Query.Contracts.PaginationListResponse<UserListItem>>> ListAsync(string? search, string? sort)
    {
        return await new Handler(database.CreateUsersQuery()).Handle(new ListQuery(_tenantId, 1, 10, null, search, sort), CancellationToken.None);
    }

    /// <summary>
    /// Alice Smith, then Bob (last name Ackroyd only), then Carol (first name only), in separate saves so their
    /// CreatedAt values differ.
    /// </summary>
    private async Task SeedAsync()
    {
        foreach (var (email, username, firstName, lastName) in new[]
                 {
                     ("alice@example.com", "Alice.Smith", "Alice", "Smith"),
                     ("bob@corp.test", "bobby", null, "Ackroyd"),
                     ("carol@example.com", "c_ross", "Carol", null)
                 })
        {
            await using var context = database.CreateContext();
            var user = User.Register(
                _tenantId, Email.Create(email).Value, Username.Create(username).Value,
                firstName is null ? null : PersonName.FromTrustedSource(firstName),
                lastName is null ? null : PersonName.FromTrustedSource(lastName)).Value;
            await new UserRepository(context).CreateAsync(user, Guid.NewGuid());
            await Task.Delay(5);
        }
    }
}
