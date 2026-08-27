namespace WardenId.Audit.Domain;

/// <summary>
/// Platform actors carry no tenant id; tenant actors always do — no other state exists,
/// so both factories are correct-by-construction and neither can fail.
/// </summary>
public sealed record ActorScope
{
    public bool IsPlatform { get; }
    public Guid? TenantId { get; }

    private ActorScope(bool isPlatform, Guid? tenantId)
    {
        IsPlatform = isPlatform;
        TenantId = tenantId;
    }

    public static ActorScope Platform() => new(true, null);

    public static ActorScope ForTenant(Guid tenantId) => new(false, tenantId);
}
