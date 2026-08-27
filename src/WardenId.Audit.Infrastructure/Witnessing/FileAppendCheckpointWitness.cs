using Microsoft.Extensions.Options;
using WardenId.Audit.Application.Abstractions;

namespace WardenId.Audit.Infrastructure.Witnessing;

/// <summary>
/// Spec §8's "append-only file on a separate volume" witness. Only registered when
/// AuditWitnessOptions.AppendOnlyFilePath is configured. A single process-local semaphore is
/// enough here — the OS append (File.AppendAllTextAsync opens with FileMode.Append) is what
/// keeps concurrent writes from a single process from interleaving; this is a best-effort
/// witness, not the authoritative record (audit.checkpoints already is), so cross-process
/// interleaving at worst produces two adjacent, individually-intact note blocks.
/// </summary>
public sealed class FileAppendCheckpointWitness(IOptions<AuditWitnessOptions> options) : IAuditCheckpointWitness
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    public async Task WitnessAsync(string signedNoteText, CancellationToken cancellationToken = default)
    {
        var path = options.Value.AppendOnlyFilePath
            ?? throw new InvalidOperationException("FileAppendCheckpointWitness requires AppendOnlyFilePath to be configured.");

        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(path, signedNoteText, cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }
}
