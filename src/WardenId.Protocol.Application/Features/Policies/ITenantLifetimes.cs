namespace WardenId.Protocol.Application.Features.Policies;

/// <summary>
/// Effective lifetimes per tenant for the issuance hot path (code, token, session start, cookie). Cached briefly
/// per tenant; <see cref="Invalidate"/> drops this node's entry after a change. Other Gateway nodes pick a change
/// up within <see cref="TenantLifetimes.CacheDuration"/>.
/// </summary>
public interface ITenantLifetimes
{
    Task<EffectiveLifetimes> GetAsync(Guid tenantId, CancellationToken cancellationToken);

    void Invalidate(Guid tenantId);
}
