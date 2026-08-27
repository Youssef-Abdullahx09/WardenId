using FluentResults;

namespace WardenId.Audit.Domain;

/// <summary>One before/after pair for an admin CRUD diff — see docs/audit/audit-module-spec.md I8.</summary>
public sealed record PropertyChange
{
    public string PropertyName { get; }
    public AuditValue? OldValue { get; }
    public AuditValue? NewValue { get; }

    private PropertyChange(string propertyName, AuditValue? oldValue, AuditValue? newValue)
    {
        PropertyName = propertyName;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public static Result<PropertyChange> Create(string propertyName, AuditValue? oldValue, AuditValue? newValue)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return Result.Fail(AuditErrors.PropertyChangeNameRequired());
        return Result.Ok(new PropertyChange(propertyName, oldValue, newValue));
    }
}
