using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Application.Features.Grants;
using WardenId.Protocol.Infrastructure.OpenIddict;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Infrastructure.Grants;

/// <summary>
/// Finds the client's rows by their owner stamp and revokes them. The stamp lives in the Properties JSON (no schema
/// change, ADR-01), so the database narrows by a substring match on the tenant id (a Guid, which JSON never escapes,
/// unlike a client_id might be) and every candidate is then checked exactly with
/// <see cref="AuthorizationOwnerProperties.Read"/>. That filter scans the valid rows; fine for an admin action, revisit
/// if the token tables grow large.
/// </summary>
public sealed class OpenIddictClientGrantRevoker(
    OpenIddictDbContext dbContext,
    ISessionAuthorizationRevoker authorizationRevoker,
    IOpenIddictTokenManager tokens,
    TimeProvider timeProvider) : IClientGrantRevoker
{
    public async Task RevokeAsync(Guid tenantId, string clientId, CancellationToken cancellationToken)
    {
        var tenant = tenantId.ToString();

        var authorizations = await dbContext.Set<OpenIddictEntityFrameworkCoreAuthorization>()
            .AsNoTracking()
            .Where(a => a.Status == Statuses.Valid
                        && a.Properties != null
                        && a.Properties.Contains(tenant))
            .Select(a => new { a.Id, a.Properties })
            .ToListAsync(cancellationToken);

        var authorizationIds = authorizations
            .Where(a => IsOwnedBy(a.Properties, tenantId, clientId))
            .Select(a => a.Id!)
            .ToList();

        // Authorization first, then its whole token family (refresh tokens, codes, access tokens).
        if (authorizationIds.Count > 0)
            await authorizationRevoker.RevokeAsync(authorizationIds, cancellationToken);

        // client_credentials access tokens: no authorization, stamped by StampClientCredentialsTokenOwner.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var standalone = await dbContext.Set<OpenIddictEntityFrameworkCoreToken>()
            .AsNoTracking()
            .Where(t => t.Authorization == null
                        && t.Status == Statuses.Valid
                        && t.Subject == clientId
                        && (t.ExpirationDate == null || t.ExpirationDate > now)
                        && t.Properties != null
                        && t.Properties.Contains(tenant))
            .Select(t => new { t.Id, t.Properties })
            .ToListAsync(cancellationToken);

        foreach (var row in standalone.Where(t => IsOwnedBy(t.Properties, tenantId, clientId)))
        {
            if (await tokens.FindByIdAsync(row.Id!, cancellationToken) is { } token)
                await tokens.TryRevokeAsync(token, cancellationToken);
        }
    }

    private static bool IsOwnedBy(string? properties, Guid tenantId, string clientId)
    {
        if (string.IsNullOrEmpty(properties)) return false;

        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(properties);
            return values is not null
                   && AuthorizationOwnerProperties.Read(values) is { } owner
                   && owner.TenantId == tenantId
                   && string.Equals(owner.ClientId, clientId, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            // A row whose owner can't be read can't be proven to be this tenant's.
            return false;
        }
    }
}
