using System.Collections.Concurrent;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application;

/// <summary>
/// Process-wide cache of each tenant's overrides (null: the tenant has none), because sign-in reads
/// the policy on every attempt. Entries live <see cref="Lifetime"/>; saving a tenant's policy
/// invalidates its entry here at once. Another Gateway instance sees the change within
/// <see cref="Lifetime"/>.
/// </summary>
public sealed class IdentityTenantPolicyCache(TimeProvider timeProvider)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    // Register takes any tenant id anonymously, so the cache must not grow without bound.
    private const int MaxEntries = 10_000;

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private readonly ConcurrentDictionary<Guid, long> _generations = new();

    public bool TryGet(Guid tenantId, out IdentityPolicyOverrides? overrides)
    {
        overrides = null;
        if (!_entries.TryGetValue(tenantId, out var entry)) return false;

        if (entry.ExpiresAt <= timeProvider.GetUtcNow() || entry.Generation != Generation(tenantId))
        {
            _entries.TryRemove(new KeyValuePair<Guid, Entry>(tenantId, entry));
            return false;
        }

        overrides = entry.Overrides;
        return true;
    }

    /// <summary>Take this before reading the database and pass it to <see cref="Set"/>.</summary>
    public long Generation(Guid tenantId) => _generations.GetValueOrDefault(tenantId);

    /// <summary>Caches a value read at <paramref name="generation"/>; one read before an invalidation is never served.</summary>
    public void Set(Guid tenantId, IdentityPolicyOverrides? overrides, long generation)
    {
        if (generation != Generation(tenantId)) return;

        if (_entries.Count >= MaxEntries)
        {
            var now = timeProvider.GetUtcNow();
            foreach (var (key, entry) in _entries)
                if (entry.ExpiresAt <= now) _entries.TryRemove(key, out _);
            if (_entries.Count >= MaxEntries) return;
        }

        _entries[tenantId] = new Entry(overrides, timeProvider.GetUtcNow().Add(Lifetime), generation);
    }

    public void Invalidate(Guid tenantId)
    {
        _generations.AddOrUpdate(tenantId, 1, (_, generation) => generation + 1);
        _entries.TryRemove(tenantId, out _);
    }

    private sealed record Entry(IdentityPolicyOverrides? Overrides, DateTimeOffset ExpiresAt, long Generation);
}
