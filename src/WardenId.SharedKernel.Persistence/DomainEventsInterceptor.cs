using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using WardenId.SharedKernel.Domain;
using DbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace WardenId.SharedKernel.Persistence;

public sealed class DomainEventsInterceptor(
    ICapPublisher capPublisher) : SaveChangesInterceptor
{
    private bool _isTransactionOwner;
 
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null) return await base.SavingChangesAsync(eventData, result, cancellationToken);
 
        IDbContextTransaction? currentTransaction = eventData.Context.Database.CurrentTransaction;
        if (currentTransaction is null)
        {
            _isTransactionOwner = true;
            await eventData.Context.Database.BeginTransactionAsync(capPublisher, autoCommit: false, cancellationToken);
        }
        else
        {
            capPublisher.Transaction =
                ActivatorUtilities.CreateInstance<PostgreSqlCapTransaction>(capPublisher.ServiceProvider);
            capPublisher.Transaction.DbTransaction = currentTransaction.GetDbTransaction();
        }
 
        var domainEvents = GetDomainEvents(eventData.Context);
        foreach (var domainEvent in domainEvents)
        {
            var name = domainEvent.GetType().Name;
            await capPublisher.PublishAsync(name, domainEvent, cancellationToken: cancellationToken);
        }
 
        ClearDomainEvents(eventData.Context);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
 
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null) return await base.SavedChangesAsync(eventData, result, cancellationToken);
 
        if (_isTransactionOwner)
        {
            await eventData.Context.Database.CommitTransactionAsync(cancellationToken);
        }
 
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
 
    private static IEnumerable<DomainEvent> GetDomainEvents(
        DbContext dbContext)
    {
        var entities = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .ToList();
 
        return entities.SelectMany(entry => entry.Entity.GetDomainEvents());
    }
 
    private static void ClearDomainEvents(
        DbContext dbContext)
    {
        var entries = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .ToList();
 
        entries.ForEach(entity => entity.Entity.ClearDomainEvents());
    }
}