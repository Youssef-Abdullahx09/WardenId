using System.Reflection;
using Microsoft.EntityFrameworkCore;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.Domain;

namespace WardenId.SharedKernel.Persistence;

public abstract class DbContextBase(DbContextOptions options) : DbContext(options), IDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyIndexOnEntityIsDeleted();
        modelBuilder.ApplyValueGeneratedNeverOnId();
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

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
            .ToList().ForEach(entry => entry.Entity.MarkAsDeleted(userId));
       
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
            .ToList().ForEach(entry => entry.Entity.MarkAsDeleted(userId));
    }
}