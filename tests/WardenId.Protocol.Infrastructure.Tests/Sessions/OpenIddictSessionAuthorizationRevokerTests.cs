using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using WardenId.Protocol.Infrastructure.OpenIddict;
using WardenId.Protocol.Infrastructure.Sessions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Infrastructure.Tests.Sessions;

public class OpenIddictSessionAuthorizationRevokerTests
{
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
        // EF InMemory has no ExecuteUpdate; OpenIddict then revokes token by token.
        services.Configure<OpenIddictEntityFrameworkCoreOptions>(options => options.DisableBulkOperations = true);
        return services.BuildServiceProvider();
    }

    private static async Task<string> SeedAuthorizationWithRefreshTokenAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();

        var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            Status = Statuses.Valid,
            Subject = "user",
            Type = AuthorizationTypes.AdHoc,
            CreationDate = DateTimeOffset.UtcNow
        });
        var authorizationId = (await authorizations.GetIdAsync(authorization))!;
        await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            AuthorizationId = authorizationId,
            Status = Statuses.Valid,
            Subject = "user",
            Type = TokenTypeHints.RefreshToken
        });
        return authorizationId;
    }

    private static async Task RevokeAsync(IServiceProvider services, params string[] authorizationIds)
    {
        using var scope = services.CreateScope();
        var revoker = new OpenIddictSessionAuthorizationRevoker(
            scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>(),
            scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>());
        await revoker.RevokeAsync(authorizationIds, CancellationToken.None);
    }

    private static async Task<(string? Authorization, string?[] Tokens)> StatusesAsync(IServiceProvider services, string authorizationId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenIddictDbContext>();
        var authorization = await db.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking()
            .SingleAsync(entity => entity.Id == authorizationId);
        var tokens = await db.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(token => token.Authorization!.Id == authorizationId)
            .Select(token => token.Status)
            .ToArrayAsync();
        return (authorization.Status, tokens);
    }

    [Fact]
    public async Task RevokeAsync_RevokesTheAuthorizationAndItsTokens()
    {
        await using var services = CreateServices();
        var authorizationId = await SeedAuthorizationWithRefreshTokenAsync(services);

        await RevokeAsync(services, authorizationId);

        var (authorization, tokens) = await StatusesAsync(services, authorizationId);
        Assert.Equal(Statuses.Revoked, authorization);
        Assert.Equal(Statuses.Revoked, Assert.Single(tokens));
    }

    [Fact]
    public async Task RevokeAsync_LeavesOtherAuthorizationsUntouched()
    {
        await using var services = CreateServices();
        var revoked = await SeedAuthorizationWithRefreshTokenAsync(services);
        var untouched = await SeedAuthorizationWithRefreshTokenAsync(services);

        await RevokeAsync(services, revoked);

        var (authorization, tokens) = await StatusesAsync(services, untouched);
        Assert.Equal(Statuses.Valid, authorization);
        Assert.Equal(Statuses.Valid, Assert.Single(tokens));
    }

    [Fact]
    public async Task RevokeAsync_IgnoresUnknownAndAlreadyRevokedIds()
    {
        await using var services = CreateServices();
        var authorizationId = await SeedAuthorizationWithRefreshTokenAsync(services);
        await RevokeAsync(services, authorizationId);

        // A CAP retry of the same message, plus an id whose rows were already pruned.
        await RevokeAsync(services, authorizationId, Guid.NewGuid().ToString());

        var (authorization, _) = await StatusesAsync(services, authorizationId);
        Assert.Equal(Statuses.Revoked, authorization);
    }
}
