namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Created { get; } = [];

    public Task CreateAsync(RefreshToken refreshToken, Guid userId, CancellationToken cancellationToken)
    {
        Created.Add(refreshToken);
        return Task.CompletedTask;
    }
}
