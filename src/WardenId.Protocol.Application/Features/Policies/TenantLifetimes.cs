using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain.TokenPolicies;

namespace WardenId.Protocol.Application.Features.Policies;

public sealed class TenantLifetimes(
    IProtocolDbContext dbContext,
    IMemoryCache cache,
    IOptions<ProtocolTokenOptions> tokenOptions,
    IOptions<ProtocolSessionOptions> sessionOptions) : ITenantLifetimes
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<EffectiveLifetimes> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // The overrides are cached, not the effective values, so a null (no row) is cached too.
        var overrides = await cache.GetOrCreateAsync(CacheKey(tenantId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            entry.Size = 1;
            return await LoadAsync(dbContext, tenantId, cancellationToken);
        });

        return EffectiveLifetimes.Resolve(tokenOptions.Value, sessionOptions.Value, overrides);
    }

    public void Invalidate(Guid tenantId) => cache.Remove(CacheKey(tenantId));

    /// <summary>The tenant's stored overrides, uncached, or null when it has none.</summary>
    public static async Task<TenantLifetimeOverrides?> LoadAsync(
        IProtocolDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var policy = await dbContext.TenantTokenPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        return policy?.Overrides;
    }

    private static string CacheKey(Guid tenantId) => ProtocolCacheKeys.TenantLifetimesPrefix + tenantId.ToString("N");
}
