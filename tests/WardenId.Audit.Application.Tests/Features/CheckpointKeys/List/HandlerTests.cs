using WardenId.Audit.Application.Features.CheckpointKeys.List;

namespace WardenId.Audit.Application.Tests.Features.CheckpointKeys.List;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllKnownKeys()
    {
        var repo = new FakeAuditProofRepository();
        var key = ChainFixture.CreateSigningKey();
        repo.Keys.Add(key.Row);

        var result = await new Handler(repo).Handle(new Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var summary = Assert.Single(result.Value);
        Assert.Equal(key.Kid, summary.Kid);
        Assert.True(summary.IsActive);
    }

    [Fact]
    public async Task Handle_NoKeysYet_ReturnsEmptyList()
    {
        var result = await new Handler(new FakeAuditProofRepository()).Handle(new Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
