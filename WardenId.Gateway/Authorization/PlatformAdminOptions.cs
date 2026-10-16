namespace WardenId.Gateway.Authorization;

/// <summary>
/// Who may call the admin API until WardenId has an authorization model (roles, B2). Section
/// <see cref="GatewayConfigurationKeys.AdminApi.Section"/>. Empty means nobody, apart from the
/// seeded master admin (<see cref="PlatformAdmins"/>).
/// </summary>
public sealed class PlatformAdminOptions
{
    public List<PlatformAdminEntry> PlatformAdmins { get; init; } = [];
}

public sealed class PlatformAdminEntry
{
    public Guid TenantId { get; init; }
    public Guid UserId { get; init; }
}
