using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WardenId.Audit.Application;
using WardenId.Audit.Domain;
using WardenId.SharedKernel.Domain;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Audit.Infrastructure;

/// <summary>
/// Generic admin.* CRUD capture (I8) — diffs ChangeTracker entries for any aggregate opting
/// in via IAuditableAggregate into a PropertyChange list, and adds the resulting
/// AuditEventRecord to the same ChangeTracker/transaction so the audit row and the business
/// change commit or fail together (spec §5). Deliberately fails closed: throws if ambient
/// context is missing, or a declared AuditTargetTypeKey isn't registered in AuditTargetType's
/// catalog — an admin action whose actor or classification can't be determined should block
/// the save, not silently persist unaudited (I10).
///
/// Only scalar properties are diffed — navigation/collection changes on child entities are
/// out of scope for this generic mechanism; a handler that needs those audited explicitly
/// builds its own AuditEvent via IAuditRecorder instead.
///
/// EntityState.Deleted never reaches here: DbContextBase.CheckAndUpdateEntities converts a
/// hard delete into IsDeleted=true + EntityState.Modified before this interceptor runs, so a
/// soft-delete surfaces as an ordinary property diff — this interceptor promotes it to a
/// distinct "delete" event type by checking specifically for IsDeleted flipping false→true,
/// rather than adding a fourth EntityState case that can't actually occur.
/// </summary>
public sealed class AuditCaptureInterceptor(IAuditContextAccessor auditContextAccessor) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext context)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => e is { Entity: IAuditableAggregate, State: EntityState.Added or EntityState.Modified })
            .ToList();

        if (entries.Count == 0) return;

        var ambient = auditContextAccessor.Current ?? throw new InvalidOperationException(
            "No ambient audit context — an admin.* write must have IAuditContextAccessor.Current set before SaveChanges.");

        foreach (var entry in entries)
        {
            var auditable = (IAuditableAggregate)entry.Entity;
            var changes = BuildPropertyChanges(entry, auditable.AuditSensitivePropertyNames);
            var eventType = ResolveEventType(entry.State, changes);
            var targetType = AuditTargetType.FromKey(auditable.AuditTargetTypeKey) ?? throw new InvalidOperationException(
                $"'{auditable.AuditTargetTypeKey}' is not a registered AuditTargetType key.");
            var channel = ResolveChannel(auditable.AuditChannel);
            var targetId = entry.Property(nameof(IEntity.Id)).CurrentValue?.ToString() ?? "";
            var target = AuditTarget.Create(targetType, targetId).Value;

            var auditEvent = AuditEvent.Create(
                Guid.CreateVersion7(),
                channel,
                eventType,
                schemaVersion: 1,
                DateTimeOffset.UtcNow,
                AuditSeverity.Info,
                ambient.Actor,
                AuditOutcome.Success(),
                ambient.Client,
                AuthContext.None(),
                ambient.Correlation,
                targets: [target],
                changes: changes).Value;

            context.Set<AuditEventRecord>().Add(AuditEventRecordFactory.Create(auditEvent, ambient.TenantId));
        }
    }

    private static List<PropertyChange> BuildPropertyChanges(EntityEntry entry, IReadOnlySet<string> sensitiveProperties)
    {
        var changes = new List<PropertyChange>();

        foreach (var property in entry.Properties)
        {
            if (entry.State == EntityState.Modified && !property.IsModified) continue;

            var isSensitive = sensitiveProperties.Contains(property.Metadata.Name);
            var oldValue = entry.State == EntityState.Added ? null : Render(property.OriginalValue, isSensitive);
            var newValue = Render(property.CurrentValue, isSensitive);

            changes.Add(PropertyChange.Create(property.Metadata.Name, oldValue, newValue).Value);
        }

        return changes;
    }

    private static AuditValue Render(object? value, bool isSensitive)
    {
        var text = value?.ToString() ?? "null";

        // An empty string carries no secret material to protect — redacting it would throw
        // (ProtectedValue.Redact rejects empty input) for no security benefit.
        if (isSensitive && text.Length > 0) return new AuditValue.Redacted(ProtectedValue.Redact(text));
        return new AuditValue.Plain(text);
    }

    private static AuditEventType ResolveEventType(EntityState state, IReadOnlyList<PropertyChange> changes)
    {
        if (state == EntityState.Added) return AuditEventType.AdminEntityCreate;

        var becameDeleted = changes.Any(c =>
            c.PropertyName == nameof(IAudit.IsDeleted) &&
            c.OldValue is AuditValue.Plain { DisplayValue: "False" } &&
            c.NewValue is AuditValue.Plain { DisplayValue: "True" });

        return becameDeleted ? AuditEventType.AdminEntityDelete : AuditEventType.AdminEntityUpdate;
    }

    private static AuditChannel ResolveChannel(AuditableChannel channel) => channel switch
    {
        AuditableChannel.AdminDirectory => AuditChannel.AdminDirectory,
        AuditableChannel.AdminConfig => AuditChannel.AdminConfig,
        _ => throw new NotSupportedException($"Unsupported auditable channel: {channel}")
    };
}
