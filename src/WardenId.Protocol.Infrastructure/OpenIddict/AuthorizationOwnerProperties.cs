using System.Text.Json;
using WardenId.Protocol.Application.Features.Grants;

namespace WardenId.Protocol.Infrastructure.OpenIddict;

/// <summary>
/// Tenant and client of an OpenIddict authorization, kept in its Properties JSON column (no schema change). OpenIddict
/// stores neither: token tables are not tenant-partitioned and the read-only application store leaves ApplicationId
/// empty (ADR-01). Written once by <see cref="Handlers.StampAuthorizationOwner"/>; read by the admin grant endpoints.
/// </summary>
public static class AuthorizationOwnerProperties
{
    public const string TenantId = "wardenid_tenant_id";
    public const string ClientId = "wardenid_client_id";

    public static void Write(IDictionary<string, JsonElement> properties, Guid tenantId, string clientId)
    {
        properties[TenantId] = JsonSerializer.SerializeToElement(tenantId.ToString());
        properties[ClientId] = JsonSerializer.SerializeToElement(clientId);
    }

    /// <summary>Null when either value is missing or malformed: such a row's tenant can't be proven.</summary>
    public static GrantOwner? Read(IReadOnlyDictionary<string, JsonElement> properties) =>
        properties.TryGetValue(TenantId, out var tenant)
        && tenant.ValueKind == JsonValueKind.String
        && Guid.TryParse(tenant.GetString(), out var tenantId)
        && properties.TryGetValue(ClientId, out var client)
        && client.ValueKind == JsonValueKind.String
        && client.GetString() is { Length: > 0 } clientId
            ? new GrantOwner(tenantId, clientId)
            : null;
}
