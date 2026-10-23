using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.Domain;
using WardenId.SharedKernel.Persistence.Utilities;

namespace WardenId.SharedKernel.Persistence;

public abstract class DbContextBase(DbContextOptions options) : DbContext(options), IDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyIndexOnEntityIsDeleted();
        modelBuilder.ApplyValueGeneratedNeverOnId();

        // Deliberately no ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly())
        // here: GetExecutingAssembly() resolves to THIS assembly (SharedKernel.Persistence),
        // not the derived DbContext's — so it would silently apply every config living here
        // (e.g. AuditEventConfiguration) to every module's context, not just the ones that
        // opt in. Each context applies its own configs explicitly instead.

        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SaveChangesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        CheckAndUpdateEntities(userId);
        return await base.SaveChangesAsync(cancellationToken);
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    public Task CommitTransactionAsync(CancellationToken cancellationToken) =>
        Database.CommitTransactionAsync(cancellationToken);

    public Task RollbackTransactionAsync(CancellationToken cancellationToken) =>
        Database.RollbackTransactionAsync(cancellationToken);

    /// <summary>
    /// Whether a removed entity stays as a soft-deleted row (the default) or is physically
    /// deleted. A module overrides this for rows that must not outlive their removal.
    /// </summary>
    protected virtual bool KeepsDeletedRow(EntityEntry<Entity> entry) => true;

    private void CheckAndUpdateEntities(Guid userId)
    {
        ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.State == EntityState.Added)
            .ToList().ForEach(entry => entry.Entity.MarkAsCreated(userId));
        ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.State == EntityState.Modified)
            .ToList().ForEach(entry => entry.Entity.MarkAsModified(userId));
        ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.State == EntityState.Deleted)
            .ToList().ForEach(entry =>
            {
                entry.Entity.MarkAsDeleted(userId);
                entry.State = EntityState.Modified;
                KeepOwnedData(entry);
            });

        ChangeTracker
            .Entries<Entity>()
            .Where(e => e.State == EntityState.Added)
            .ToList().ForEach(entry => entry.Entity.MarkAsCreated(userId));
        ChangeTracker
            .Entries<Entity>()
            .Where(e => e.State == EntityState.Modified)
            .ToList().ForEach(entry => entry.Entity.MarkAsModified(userId));
        ChangeTracker
            .Entries<Entity>()
            .Where(e => e.State == EntityState.Deleted)
            .ToList()
            .Where(KeepsDeletedRow)
            .ToList().ForEach(entry =>
            {
                entry.Entity.MarkAsDeleted(userId);
                entry.State = EntityState.Modified;
                KeepOwnedData(entry);
            });
    }

    // Removing an entity cascades to its owned types, which aren't Entity and so aren't turned
    // into a soft delete above: EF would null an owned reference's columns in the owner's row
    // (failing on NOT NULL ones) and delete owned collection rows. A soft-deleted entity keeps
    // all of its data, so its owned entries go back to Unchanged.
    private static void KeepOwnedData(EntityEntry entry)
    {
        foreach (var navigation in entry.Navigations)
        {
            if (navigation.Metadata is not INavigation { TargetEntityType: var target } || !target.IsOwned()) continue;

            var owned = navigation switch
            {
                CollectionEntry collection => collection.CurrentValue?.Cast<object>() ?? [],
                ReferenceEntry { CurrentValue: { } value } => [value],
                _ => []
            };

            foreach (var ownedEntry in owned.Select(entry.Context.Entry).Where(e => e.State == EntityState.Deleted))
            {
                ownedEntry.State = EntityState.Unchanged;
                KeepOwnedData(ownedEntry);
            }
        }
    }
}