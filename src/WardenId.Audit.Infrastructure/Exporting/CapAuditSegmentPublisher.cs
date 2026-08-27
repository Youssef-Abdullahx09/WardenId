using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Exporting;

/// <summary>
/// Enlists the CAP publish onto the caller's already-open EF transaction — mirrors
/// DomainEventsInterceptor's "transaction already exists" branch exactly (SharedKernel.Persistence),
/// rather than the "become the transaction owner" branch, since AuditExporterService opens and
/// owns the transaction itself.
/// </summary>
public sealed class CapAuditSegmentPublisher(ICapPublisher capPublisher) : IAuditSegmentPublisher
{
    private const string TopicName = nameof(AuditSegmentExportedMessage);

    public async Task PublishAsync(AuditDbContext db, AuditSegmentExportedMessage message, CancellationToken cancellationToken = default)
    {
        var currentTransaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("PublishAsync requires an already-open EF transaction to enlist CAP's outbox insert onto.");

        capPublisher.Transaction = ActivatorUtilities.CreateInstance<PostgreSqlCapTransaction>(capPublisher.ServiceProvider);
        capPublisher.Transaction.DbTransaction = currentTransaction.GetDbTransaction();

        await capPublisher.PublishAsync(TopicName, message, cancellationToken: cancellationToken);
    }
}
