namespace WardenId.Protocol.Infrastructure.Sessions;

/// <summary>Deletes sessions that ended or expired before a threshold.</summary>
public interface ISessionPruner
{
    Task PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken);
}
