namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeJwtIssuer : IJwtIssuer
{
    public ActiveSigningKey? LastKey { get; private set; }

    public string IssueAccessToken(ActiveSigningKey key, Guid tenantId, Guid userId, DateTimeOffset issuedAtUtc, TimeSpan lifetime)
    {
        LastKey = key;
        return $"access:{userId}";
    }

    public string IssueIdToken(
        ActiveSigningKey key, Guid tenantId, Guid userId, string email, bool emailVerified, string username,
        DateTimeOffset issuedAtUtc, TimeSpan lifetime)
    {
        LastKey = key;
        return $"id:{userId}";
    }
}
