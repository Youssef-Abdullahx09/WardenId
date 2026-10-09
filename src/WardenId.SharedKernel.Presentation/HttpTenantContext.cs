using Microsoft.AspNetCore.Http;
using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.SharedKernel.Presentation;

/// <summary>
/// Backed by HttpContext.Items so it is per request while staying a singleton — usable from
/// singleton handlers (e.g. OpenIddict's) as well as scoped services. Outside a request it reads
/// null and refuses writes.
/// </summary>
public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    private const string SlugKey = "warden:slug";
    private const string TenantIdKey = "warden:tenant_id";

    public string? Slug => httpContextAccessor.HttpContext?.Items[SlugKey] as string;

    public Guid? TenantId => httpContextAccessor.HttpContext?.Items[TenantIdKey] as Guid?;

    public void SetSlug(string slug) => Items[SlugKey] = slug;

    public void SetTenantId(Guid tenantId) => Items[TenantIdKey] = tenantId;

    private IDictionary<object, object?> Items =>
        httpContextAccessor.HttpContext?.Items
        ?? throw new InvalidOperationException("The tenant can only be set within an HTTP request.");
}
