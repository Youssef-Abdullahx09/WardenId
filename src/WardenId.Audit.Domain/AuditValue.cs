namespace WardenId.Audit.Domain;

/// <summary>
/// Closed set of shapes a captured field value can take. Enforces I9 structurally: a
/// sensitive value can only ever be a Redacted wrapping a ProtectedValue — there is no
/// variant that carries a raw string for a field the caller meant to protect.
/// </summary>
public abstract record AuditValue
{
    public sealed record Plain(string DisplayValue) : AuditValue;

    public sealed record Redacted(ProtectedValue Value) : AuditValue;
}
