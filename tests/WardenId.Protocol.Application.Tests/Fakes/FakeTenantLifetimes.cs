using WardenId.Protocol.Application.Features.Policies;
using WardenId.Protocol.Domain.TokenPolicies;

namespace WardenId.Protocol.Application.Tests.Fakes;

/// <summary>Resolves from the given options and tenant overrides; no database, no cache.</summary>
public sealed class FakeTenantLifetimes(
    ProtocolTokenOptions? tokens = null, ProtocolSessionOptions? sessions = null, TenantLifetimeOverrides? overrides = null)
    : ITenantLifetimes
{
    public List<Guid> Requested { get; } = [];
    public List<Guid> Invalidated { get; } = [];

    public Task<EffectiveLifetimes> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        Requested.Add(tenantId);
        return Task.FromResult(EffectiveLifetimes.Resolve(
            tokens ?? new ProtocolTokenOptions(), sessions ?? new ProtocolSessionOptions(), overrides));
    }

    public void Invalidate(Guid tenantId) => Invalidated.Add(tenantId);
}
