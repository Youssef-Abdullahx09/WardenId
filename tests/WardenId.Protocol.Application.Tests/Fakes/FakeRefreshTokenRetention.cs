namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeRefreshTokenRetention(TimeSpan? minimumRetention) : IRefreshTokenRetention
{
    public TimeSpan? MinimumRetention { get; } = minimumRetention;
}
