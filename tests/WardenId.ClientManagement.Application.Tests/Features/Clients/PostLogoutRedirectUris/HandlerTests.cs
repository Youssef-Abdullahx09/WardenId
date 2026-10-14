using Microsoft.EntityFrameworkCore;
using AddCommand = WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Add.Command;
using AddHandler = WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Add.Handler;
using RemoveCommand = WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Remove.Command;
using RemoveHandler = WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Remove.Handler;
using ReplaceCommand = WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Replace.Command;
using ReplaceHandler = WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Replace.Handler;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.PostLogoutRedirectUris;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static async Task<string[]> StoredUrisAsync(TestClientManagementDbContext db, Guid id)
    {
        db.ChangeTracker.Clear();
        var client = await db.Clients.SingleAsync(c => c.Id == id);
        return client.PostLogoutRedirectUris.Select(u => u.Value).OrderBy(v => v, StringComparer.Ordinal).ToArray();
    }

    private static void AssertErrorCode(Result result, string code) =>
        Assert.Equal(code, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);

    [Fact]
    public async Task Add_ValidUri_PersistsIt()
    {
        var client = ClientBuilder.Public(TenantId);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new AddHandler(new InMemoryClientRepository(db))
            .Handle(new AddCommand(TenantId, client.Id, "https://spa.example/logged-out"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "https://spa.example/logged-out" }, await StoredUrisAsync(db, client.Id));
    }

    [Fact]
    public async Task Add_SameUriTwice_StoresItOnce()
    {
        var client = ClientBuilder.Public(TenantId);
        await using var db = await ClientBuilder.SeedAsync(client);
        var handler = new AddHandler(new InMemoryClientRepository(db));

        await handler.Handle(new AddCommand(TenantId, client.Id, "https://spa.example/logged-out"), CancellationToken.None);
        db.ChangeTracker.Clear();
        await handler.Handle(new AddCommand(TenantId, client.Id, "https://spa.example/logged-out"), CancellationToken.None);

        Assert.Single(await StoredUrisAsync(db, client.Id));
    }

    [Fact]
    public async Task Add_InvalidUri_FailsValidation_AndPersistsNothing()
    {
        var client = ClientBuilder.Public(TenantId);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new AddHandler(new InMemoryClientRepository(db))
            .Handle(new AddCommand(TenantId, client.Id, "https://spa.example/logged-out#x"), CancellationToken.None);

        Assert.True(result.IsFailed);
        AssertErrorCode(result, ClientErrorCodes.Validation);
        Assert.Empty(await StoredUrisAsync(db, client.Id));
    }

    [Fact]
    public async Task Add_ClientOfOtherTenant_ReturnsNotFound()
    {
        var client = ClientBuilder.Public(TenantId);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new AddHandler(new InMemoryClientRepository(db))
            .Handle(new AddCommand(Guid.NewGuid(), client.Id, "https://spa.example/logged-out"), CancellationToken.None);

        AssertErrorCode(result, ClientErrorCodes.NotFound);
    }

    [Fact]
    public async Task Remove_RegisteredUri_RemovesIt()
    {
        var client = ClientBuilder.Public(TenantId);
        client.AddPostLogoutRedirectUri(RedirectUri.Create("https://spa.example/a").Value);
        client.AddPostLogoutRedirectUri(RedirectUri.Create("https://spa.example/b").Value);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new RemoveHandler(new InMemoryClientRepository(db))
            .Handle(new RemoveCommand(TenantId, client.Id, "https://spa.example/a"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "https://spa.example/b" }, await StoredUrisAsync(db, client.Id));
    }

    [Fact]
    public async Task Replace_ReplacesWholeSet()
    {
        var client = ClientBuilder.Public(TenantId);
        client.AddPostLogoutRedirectUri(RedirectUri.Create("https://spa.example/a").Value);
        client.AddPostLogoutRedirectUri(RedirectUri.Create("https://spa.example/b").Value);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new ReplaceHandler(new InMemoryClientRepository(db)).Handle(
            new ReplaceCommand(TenantId, client.Id, ["https://spa.example/b", "https://spa.example/c"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "https://spa.example/b", "https://spa.example/c" }, await StoredUrisAsync(db, client.Id));
    }

    [Fact]
    public async Task Replace_WithEmptyCollection_ClearsAll()
    {
        var client = ClientBuilder.Public(TenantId);
        client.AddPostLogoutRedirectUri(RedirectUri.Create("https://spa.example/a").Value);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new ReplaceHandler(new InMemoryClientRepository(db))
            .Handle(new ReplaceCommand(TenantId, client.Id, []), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(await StoredUrisAsync(db, client.Id));
    }

    [Fact]
    public async Task Replace_OneInvalidEntry_FailsValidation_AndChangesNothing()
    {
        var client = ClientBuilder.Public(TenantId);
        client.AddPostLogoutRedirectUri(RedirectUri.Create("https://spa.example/a").Value);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new ReplaceHandler(new InMemoryClientRepository(db)).Handle(
            new ReplaceCommand(TenantId, client.Id, ["https://spa.example/c", "not a uri"]),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        AssertErrorCode(result, ClientErrorCodes.Validation);
        Assert.Equal(new[] { "https://spa.example/a" }, await StoredUrisAsync(db, client.Id));
    }
}
