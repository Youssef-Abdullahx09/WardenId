using WardenId.Audit.Application.Abstractions;

namespace WardenId.Audit.Infrastructure.Tests.Fakes;

public sealed class FakeAuditCheckpointWitness : IAuditCheckpointWitness
{
    public List<string> Witnessed { get; } = [];
    public bool ThrowOnWitness { get; set; }

    public Task WitnessAsync(string signedNoteText, CancellationToken cancellationToken = default)
    {
        if (ThrowOnWitness) throw new InvalidOperationException("Simulated witness failure.");

        Witnessed.Add(signedNoteText);
        return Task.CompletedTask;
    }
}
