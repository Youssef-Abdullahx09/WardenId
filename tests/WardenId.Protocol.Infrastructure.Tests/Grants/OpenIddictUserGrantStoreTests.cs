using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using WardenId.Protocol.Application.Features.Grants;
using WardenId.Protocol.Infrastructure.Grants;
using WardenId.Protocol.Infrastructure.OpenIddict;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Infrastructure.Tests.Grants;

public class OpenIddictUserGrantStoreTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly string Subject = Guid.NewGuid().ToString();
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<OpenIddictDbContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName);
            options.UseOpenIddict();
        });
        services.AddOpenIddict().AddCore(options => options.UseEntityFrameworkCore().UseDbContext<OpenIddictDbContext>());
        return services.BuildServiceProvider();
    }

    private static async Task<string> SeedAsync(
        IServiceProvider services, string subject, string status = Statuses.Valid, GrantOwner? owner = null,
        params string[] scopes)
    {
        using var scope = services.CreateScope();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            Status = status,
            Subject = subject,
            Type = AuthorizationTypes.AdHoc,
            CreationDate = CreatedAt
        };
        descriptor.Scopes.UnionWith(scopes);
        if (owner is not null)
            AuthorizationOwnerProperties.Write(descriptor.Properties, owner.TenantId, owner.ClientId);

        var authorization = await authorizations.CreateAsync(descriptor);
        return (await authorizations.GetIdAsync(authorization))!;
    }

    private static async Task<T> WithStoreAsync<T>(IServiceProvider services, Func<OpenIddictUserGrantStore, Task<T>> action)
    {
        using var scope = services.CreateScope();
        return await action(new OpenIddictUserGrantStore(
            scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>()));
    }

    [Fact]
    public async Task ListBySubject_MapsStampedRow()
    {
        await using var services = CreateServices();
        var id = await SeedAsync(services, Subject, Statuses.Valid, new GrantOwner(TenantId, "web-rt"), "openid", "offline_access");

        var grants = await WithStoreAsync(services, store => store.ListBySubjectAsync(Subject, CancellationToken.None));

        var grant = Assert.Single(grants);
        Assert.Equal(id, grant.AuthorizationId);
        Assert.Equal(Subject, grant.Subject);
        Assert.Equal(new GrantOwner(TenantId, "web-rt"), grant.Owner);
        Assert.Equal(CreatedAt, grant.CreatedAt);
        Assert.Equal(new[] { "offline_access", "openid" }, grant.Scopes.Order());
        Assert.Equal(GrantStatus.Valid, grant.Status);
    }

    [Fact]
    public async Task ListBySubject_OnlyThatSubject_AnyStatus_UnstampedWithoutOwner()
    {
        await using var services = CreateServices();
        var revoked = await SeedAsync(services, Subject, Statuses.Revoked, new GrantOwner(TenantId, "web"));
        var unstamped = await SeedAsync(services, Subject);
        await SeedAsync(services, Guid.NewGuid().ToString(), Statuses.Valid, new GrantOwner(TenantId, "web"));

        var grants = await WithStoreAsync(services, store => store.ListBySubjectAsync(Subject, CancellationToken.None));

        Assert.Equal(2, grants.Count);
        Assert.Equal(GrantStatus.Revoked, grants.Single(g => g.AuthorizationId == revoked).Status);
        Assert.Null(grants.Single(g => g.AuthorizationId == unstamped).Owner);
    }

    [Theory]
    [InlineData(Statuses.Valid, GrantStatus.Valid)]
    [InlineData(Statuses.Revoked, GrantStatus.Revoked)]
    [InlineData(Statuses.Inactive, GrantStatus.Inactive)]
    [InlineData(Statuses.Rejected, GrantStatus.Rejected)]
    public async Task Find_MapsStatus(string status, GrantStatus expected)
    {
        await using var services = CreateServices();
        var id = await SeedAsync(services, Subject, status, new GrantOwner(TenantId, "web"));

        var grant = await WithStoreAsync(services, store => store.FindAsync(id, CancellationToken.None));

        Assert.Equal(expected, grant!.Status);
    }

    [Fact]
    public async Task Find_UnknownId_ReturnsNull()
    {
        await using var services = CreateServices();

        var grant = await WithStoreAsync(services, store => store.FindAsync(Guid.NewGuid().ToString(), CancellationToken.None));

        Assert.Null(grant);
    }

    [Fact]
    public void Read_IgnoresMalformedStamp()
    {
        var properties = new Dictionary<string, System.Text.Json.JsonElement>
        {
            [AuthorizationOwnerProperties.TenantId] = System.Text.Json.JsonSerializer.SerializeToElement("not-a-guid"),
            [AuthorizationOwnerProperties.ClientId] = System.Text.Json.JsonSerializer.SerializeToElement("web")
        };

        Assert.Null(AuthorizationOwnerProperties.Read(properties));
    }
}
