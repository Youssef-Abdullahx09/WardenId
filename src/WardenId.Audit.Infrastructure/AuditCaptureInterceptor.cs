using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
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
/// The aggregate's own scalar properties are always diffed. Children (child entities, owned
/// collections and references) are diffed only for the navigations the aggregate lists in
/// IAuditableAggregate.AuditedNavigationNames, as "{Navigation}[{child key}].{Property}" — so a
/// child holding secrets (a user's credentials, MFA factors) never lands in the log by accident.
/// A save that changes only opted-in children still produces an update event for the aggregate.
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
        var pending = context.ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditableAggregate)
            .Select(e => (Entry: e, Auditable: (IAuditableAggregate)e.Entity))
            .Where(p => p.Entry.State is EntityState.Added or EntityState.Modified
                        || (p.Entry.State == EntityState.Unchanged && p.Auditable.AuditedNavigationNames.Count > 0))
            .Select(p => (p.Entry, p.Auditable, Changes: BuildChanges(context, p.Entry, p.Auditable)))
            .Where(p => p.Entry.State != EntityState.Unchanged || p.Changes.Count > 0)
            .ToList();

        if (pending.Count == 0) return;

        var ambient = auditContextAccessor.Current ?? throw new InvalidOperationException(
            "No ambient audit context — an admin.* write must have IAuditContextAccessor.Current set before SaveChanges.");

        foreach (var (entry, auditable, changes) in pending)
        {
            var eventType = ResolveEventType(entry.State, changes);
            var targetType = AuditTargetType.FromKey(auditable.AuditTargetTypeKey) ?? throw new InvalidOperationException(
                $"'{auditable.AuditTargetTypeKey}' is not a registered AuditTargetType key.");
            var channel = ResolveChannel(auditable.AuditChannel);
            var targetId = auditable.AuditTargetId ?? entry.Property(nameof(IEntity.Id)).CurrentValue?.ToString() ?? "";
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

    private static List<PropertyChange> BuildChanges(DbContext context, EntityEntry entry, IAuditableAggregate auditable)
    {
        var changes = entry.State == EntityState.Unchanged
            ? []
            : BuildPropertyChanges(entry, auditable.AuditSensitivePropertyNames);

        foreach (var navigationName in auditable.AuditedNavigationNames)
            changes.AddRange(BuildChildChanges(context, entry, navigationName, auditable.AuditSensitivePropertyNames));

        return changes;
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

    /// <summary>
    /// Diffs the added, modified and deleted children of one navigation. Children are found through the
    /// change tracker rather than the navigation's current value, because a removed child is no longer in
    /// it. An owned value replaced by an equal-keyed one (a new owned reference instance) shows up as a
    /// deleted plus an added entry for the same key; they are merged into one old/new pair per property.
    /// </summary>
    private static IEnumerable<PropertyChange> BuildChildChanges(
        DbContext context, EntityEntry owner, string navigationName, IReadOnlySet<string> sensitiveProperties)
    {
        var navigation = owner.Metadata.FindNavigation(navigationName) ?? throw new InvalidOperationException(
            $"'{owner.Metadata.DisplayName()}' lists '{navigationName}' in AuditedNavigationNames, but it is not a navigation.");

        var foreignKey = navigation.ForeignKey;
        var ownerKey = foreignKey.PrincipalKey.Properties.Select(p => owner.Property(p.Name).CurrentValue).ToList();

        var children = context.ChangeTracker.Entries()
            .Where(e => e.Metadata == navigation.TargetEntityType
                        && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                        && BelongsTo(e, foreignKey.Properties, ownerKey))
            .ToList();

        // The key is in the change's name, so key properties are left out — unless the key is all the
        // child has (an owned value such as a redirect URI, keyed by owner + value).
        var foreignKeyNames = foreignKey.Properties.Select(p => p.Name).ToHashSet();
        var primaryKeyNames = (navigation.TargetEntityType.FindPrimaryKey()?.Properties ?? []).Select(p => p.Name).ToHashSet();
        var hasNonKeyProperties = navigation.TargetEntityType.GetProperties()
            .Any(p => !foreignKeyNames.Contains(p.Name) && !primaryKeyNames.Contains(p.Name));
        var skipped = hasNonKeyProperties ? foreignKeyNames.Union(primaryKeyNames).ToHashSet() : foreignKeyNames;

        foreach (var group in children.GroupBy(child => ChildLabel(navigation, child)))
        {
            var before = group.FirstOrDefault(e => e.State is EntityState.Deleted or EntityState.Modified);
            var after = group.FirstOrDefault(e => e.State is EntityState.Added or EntityState.Modified);

            foreach (var property in navigation.TargetEntityType.GetProperties().Where(p => !skipped.Contains(p.Name)))
            {
                if (before is { State: EntityState.Modified } && !before.Property(property.Name).IsModified) continue;

                var oldRaw = before?.Property(property.Name).OriginalValue;
                var newRaw = after?.Property(property.Name).CurrentValue;
                if (before is not null && after is not null && RenderRaw(oldRaw) == RenderRaw(newRaw)) continue;

                var isSensitive = sensitiveProperties.Contains($"{navigationName}.{property.Name}");
                yield return PropertyChange.Create(
                    $"{group.Key}.{property.Name}",
                    before is null ? null : Render(oldRaw, isSensitive),
                    after is null ? null : Render(newRaw, isSensitive)).Value;
            }
        }
    }

    private static bool BelongsTo(EntityEntry child, IReadOnlyList<IProperty> foreignKey, List<object?> ownerKey)
    {
        for (var i = 0; i < foreignKey.Count; i++)
        {
            var property = child.Property(foreignKey[i].Name);
            var value = child.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue;
            if (!Equals(value, ownerKey[i])) return false;
        }

        return true;
    }

    /// <summary>"Keys[{id}]" for a collection child, keyed by its primary key minus the owner's foreign key; "Limits" for a reference.</summary>
    private static string ChildLabel(INavigation navigation, EntityEntry child)
    {
        if (!navigation.IsCollection) return navigation.Name;

        var foreignKey = navigation.ForeignKey.Properties.Select(p => p.Name).ToHashSet();
        var keyParts = (navigation.TargetEntityType.FindPrimaryKey()?.Properties ?? [])
            .Where(p => !foreignKey.Contains(p.Name))
            .Select(p => RenderRaw(child.State == EntityState.Deleted
                ? child.Property(p.Name).OriginalValue
                : child.Property(p.Name).CurrentValue));

        return $"{navigation.Name}[{string.Join(",", keyParts)}]";
    }

    private static AuditValue Render(object? value, bool isSensitive)
    {
        var text = RenderRaw(value);

        // An empty string carries no secret material to protect — redacting it would throw
        // (ProtectedValue.Redact rejects empty input) for no security benefit.
        if (isSensitive && text.Length > 0) return new AuditValue.Redacted(ProtectedValue.Redact(text));
        return new AuditValue.Plain(text);
    }

    // Bytes (key material) as base64 so a sensitive value's hash covers the actual content, a collection
    // (assigned scope ids) as its items — ToString() would give the type name for both.
    private static string RenderRaw(object? value) => value switch
    {
        null => "null",
        byte[] bytes => Convert.ToBase64String(bytes),
        string text => text,
        System.Collections.IEnumerable items => $"[{string.Join(", ", items.Cast<object?>().Select(RenderRaw))}]",
        _ => value.ToString() ?? "null"
    };

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
