namespace WardenId.SharedKernel.Application.Abstractions;

/// <summary>
/// The tenant resolved for the current request, before (or without) authentication — e.g. from
/// the /tenants/{slug}/ issuer path. Unlike ICurrentTenant (route value + TenantId claim of the
/// authenticated caller), values here are set explicitly by whatever resolves the tenant and are
/// null until then. Slug is set first (cheap, from the path); TenantId once the slug is verified.
/// </summary>
public interface ITenantContext
{
    string? Slug { get; }
    Guid? TenantId { get; }

    void SetSlug(string slug);
    void SetTenantId(Guid tenantId);
}
