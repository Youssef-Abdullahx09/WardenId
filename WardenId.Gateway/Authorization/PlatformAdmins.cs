using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace WardenId.Gateway.Authorization;

/// <summary>
/// The users allowed on the admin API: the configured <see cref="PlatformAdminOptions"/> plus the
/// master admin the seeder created or found at startup. Keyed by tenant and user, so a token
/// counts only when its own tenant issued it for that user.
/// </summary>
public sealed class PlatformAdmins
{
    private readonly ConcurrentDictionary<(Guid TenantId, Guid UserId), byte> _admins = new();

    public PlatformAdmins(IOptions<PlatformAdminOptions> options)
    {
        foreach (var admin in options.Value.PlatformAdmins)
        {
            Add(admin.TenantId, admin.UserId);
        }
    }

    public void Add(Guid tenantId, Guid userId) => _admins.TryAdd((tenantId, userId), 0);

    public bool Contains(Guid tenantId, Guid userId) => _admins.ContainsKey((tenantId, userId));
}
