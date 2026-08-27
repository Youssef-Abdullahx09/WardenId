using WardenId.Audit.Infrastructure.Sealing;

namespace WardenId.Audit.Infrastructure.Tests.Fakes;

public sealed class FakeAuditCheckpointSigner : IAuditCheckpointSigner
{
    public int CallCount { get; private set; }

    public Task<(string Kid, byte[] Signature)> SignAsync(byte[] unsignedBody, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(("fake-kid", (byte[])[1, 2, 3, 4]));
    }
}
