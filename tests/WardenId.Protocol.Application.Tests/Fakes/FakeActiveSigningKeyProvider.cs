namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeActiveSigningKeyProvider : IActiveSigningKeyProvider
{
    public Result<ActiveSigningKey>? Response { get; set; }

    public Task<Result<ActiveSigningKey>> GetActiveSigningKeyAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Response ?? throw new InvalidOperationException("Response not configured."));
}
