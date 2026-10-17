using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Features.Policies;
using WardenId.Protocol.Domain.TokenPolicies;

namespace WardenId.Protocol.Application.Tests.Features.Policies;

public class TenantLifetimesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static TenantLifetimes Create(TestProtocolDbContext db, IMemoryCache cache) =>
        new(db, cache,
            Options.Create(new ProtocolTokenOptions { AccessTokenLifetime = TimeSpan.FromMinutes(10) }),
            Options.Create(new ProtocolSessionOptions { CookieLifetime = TimeSpan.FromHours(2) }));

    private static TenantLifetimeOverrides Access(int minutes) =>
        TenantLifetimeOverrides.Create(TimeSpan.FromMinutes(minutes), null, null, null, null, null, null).Value;

    [Fact]
    public async Task GetAsync_WithoutPolicy_ReturnsGlobalDefaults()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());

        var lifetimes = await Create(db, new MemoryCache(new MemoryCacheOptions())).GetAsync(TenantId, CancellationToken.None);

        Assert.False(lifetimes.IsTenantScoped);
        Assert.Equal(TimeSpan.FromMinutes(10), lifetimes.AccessToken);
        Assert.Equal(TimeSpan.FromHours(2), lifetimes.SessionCookie);
        Assert.Equal(TimeSpan.FromDays(90), lifetimes.RefreshTokenAbsolute);
    }

    [Fact]
    public async Task GetAsync_TenantOverrideWins_OverGlobal_OnlyForTheFieldsItSets()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());
        db.TenantTokenPolicies.Add(TenantTokenPolicy.Create(TenantId, Access(3)));
        await db.SaveChangesAsync();

        var lifetimes = await Create(db, new MemoryCache(new MemoryCacheOptions())).GetAsync(TenantId, CancellationToken.None);

        Assert.True(lifetimes.IsTenantScoped);
        Assert.Equal(TimeSpan.FromMinutes(3), lifetimes.AccessToken);
        Assert.Equal(TimeSpan.FromMinutes(15), lifetimes.IdToken);
        Assert.Equal(TimeSpan.FromHours(2), lifetimes.SessionCookie);
    }

    [Fact]
    public async Task GetAsync_OtherTenantsPolicy_DoesNotApply()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());
        db.TenantTokenPolicies.Add(TenantTokenPolicy.Create(Guid.NewGuid(), Access(3)));
        await db.SaveChangesAsync();

        var lifetimes = await Create(db, new MemoryCache(new MemoryCacheOptions())).GetAsync(TenantId, CancellationToken.None);

        Assert.Equal(TimeSpan.FromMinutes(10), lifetimes.AccessToken);
    }

    [Fact]
    public async Task GetAsync_IsCached_UntilInvalidated()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());
        var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = Create(db, cache);
        await resolver.GetAsync(TenantId, CancellationToken.None);

        db.TenantTokenPolicies.Add(TenantTokenPolicy.Create(TenantId, Access(3)));
        await db.SaveChangesAsync();
        Assert.Equal(TimeSpan.FromMinutes(10), (await resolver.GetAsync(TenantId, CancellationToken.None)).AccessToken);

        resolver.Invalidate(TenantId);
        Assert.Equal(TimeSpan.FromMinutes(3), (await resolver.GetAsync(TenantId, CancellationToken.None)).AccessToken);
    }

    [Fact]
    public void ClientOverride_WinsOverTenantAndGlobal()
    {
        var lifetimes = EffectiveLifetimes.Resolve(new ProtocolTokenOptions(), new ProtocolSessionOptions(), Access(3));

        Assert.Equal(TimeSpan.FromSeconds(60), lifetimes.AccessTokenFor(60));
        Assert.Equal(TimeSpan.FromMinutes(3), lifetimes.AccessTokenFor(null));
        Assert.Equal(TimeSpan.FromSeconds(120), lifetimes.IdTokenFor(120));
        Assert.Equal(TimeSpan.FromHours(5), lifetimes.RefreshTokenFor(5 * 3600));
        Assert.Equal(TimeSpan.FromDays(30), lifetimes.RefreshTokenFor(null));
    }
}
