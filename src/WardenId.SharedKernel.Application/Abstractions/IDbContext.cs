namespace WardenId.SharedKernel.Application.Abstractions;

public interface IDbContext
{
    Task<int> SaveChangesAsync(Guid userId, CancellationToken cancellationToken);
    Task BeginTransactionAsync(CancellationToken cancellationToken);
    Task CommitTransactionAsync(CancellationToken cancellationToken);
    Task RollbackTransactionAsync(CancellationToken cancellationToken);
}