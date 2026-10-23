namespace WardenId.Protocol.Application.Utilities;

/// <summary>IMemoryCache key prefixes; the cached item's id or slug is appended.</summary>
public static class ProtocolCacheKeys
{
    public const string TenantLifetimesPrefix = "wardenid:protocol-tenant-lifetimes:";
    public const string CorsOriginsPrefix = "wardenid:protocol-cors-origins:";
}
