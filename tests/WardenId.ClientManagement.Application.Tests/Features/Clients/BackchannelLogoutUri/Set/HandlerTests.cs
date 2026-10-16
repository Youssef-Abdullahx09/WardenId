using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Features.Clients.BackchannelLogoutUri.Set;
using DomainBackchannelLogoutUri = WardenId.ClientManagement.Domain.BackchannelLogoutUri;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.BackchannelLogoutUri.Set;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static async Task<string?> StoredUriAsync(TestClientManagementDbContext db, Guid id)
    {
        db.ChangeTracker.Clear();
        var client = await db.Clients.SingleAsync(c => c.Id == id);
        return client.BackchannelLogoutUri?.Value;
    }

    [Fact]
    public async Task Handle_ValidHttpsUri_PersistsIt()
    {
        var client = ClientBuilder.Confidential(TenantId, GrantType.AuthorizationCode);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(new InMemoryClientRepository(db))
            .Handle(new Command(TenantId, client.Id, "https://app.example/bcl", ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://app.example/bcl", await StoredUriAsync(db, client.Id));
    }

    [Fact]
    public async Task Handle_Null_ClearsIt()
    {
        var client = ClientBuilder.Confidential(TenantId, GrantType.AuthorizationCode);
        client.SetBackchannelLogoutUri(DomainBackchannelLogoutUri.Create("https://app.example/bcl").Value);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(new InMemoryClientRepository(db))
            .Handle(new Command(TenantId, client.Id, null, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(await StoredUriAsync(db, client.Id));
    }

    [Fact]
    public async Task Handle_HttpNonLoopback_FailsValidation_AndKeepsOldValue()
    {
        var client = ClientBuilder.Confidential(TenantId, GrantType.AuthorizationCode);
        client.SetBackchannelLogoutUri(DomainBackchannelLogoutUri.Create("https://app.example/bcl").Value);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(new InMemoryClientRepository(db))
            .Handle(new Command(TenantId, client.Id, "http://app.example/bcl", ActorId), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrorCodes.Validation, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);
        Assert.Equal("https://app.example/bcl", await StoredUriAsync(db, client.Id));
    }

    [Fact]
    public async Task Handle_UnknownClient_ReturnsNotFound()
    {
        await using var db = await ClientBuilder.SeedAsync(ClientBuilder.Confidential(TenantId));

        var result = await new Handler(new InMemoryClientRepository(db))
            .Handle(new Command(TenantId, Guid.NewGuid(), "https://app.example/bcl", ActorId), CancellationToken.None);

        Assert.Equal(ClientErrorCodes.NotFound, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);
    }
}
