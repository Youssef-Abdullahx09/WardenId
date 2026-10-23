using WardenId.ClientManagement.Application.Features.Clients.ListCorsOrigins;
using WardenId.ClientManagement.Integration.Command.Features.Clients.ListCorsOrigins;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.ListCorsOrigins;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Client WithOrigins(Client client, params string[] origins)
    {
        foreach (var origin in origins) client.AddCorsOrigin(CorsOrigin.Create(origin).Value);
        return client;
    }

    [Fact]
    public async Task Handle_ReturnsTheDistinctUnion_OfTheTenantsEnabledClients()
    {
        await using var db = await ClientBuilder.SeedAsync(
            WithOrigins(ClientBuilder.Public(TenantId), "https://spa.example", "https://admin.example"),
            WithOrigins(ClientBuilder.Public(TenantId), "https://spa.example"));

        var result = await new Handler(db).Handle(new Query(TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "https://admin.example", "https://spa.example" }, result.Value);
    }

    [Fact]
    public async Task Handle_SkipsOtherTenants_DisabledAndDeletedClients()
    {
        var disabled = WithOrigins(ClientBuilder.Public(TenantId), "https://disabled.example");
        disabled.Disable();
        var deleted = WithOrigins(ClientBuilder.Public(TenantId), "https://deleted.example");
        deleted.Delete();
        await using var db = await ClientBuilder.SeedAsync(
            WithOrigins(ClientBuilder.Public(Guid.NewGuid()), "https://other-tenant.example"),
            disabled,
            deleted,
            WithOrigins(ClientBuilder.Public(TenantId), "https://spa.example"));

        var result = await new Handler(db).Handle(new Query(TenantId), CancellationToken.None);

        Assert.Equal(new[] { "https://spa.example" }, result.Value);
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_ForATenantWithoutOrigins()
    {
        await using var db = await ClientBuilder.SeedAsync(ClientBuilder.Confidential(TenantId));

        var result = await new Handler(db).Handle(new Query(TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
