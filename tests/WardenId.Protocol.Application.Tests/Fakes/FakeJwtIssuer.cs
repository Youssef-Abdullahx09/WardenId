namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeJwtIssuer : IJwtIssuer
{
    public string IssueAccessToken(ActiveSigningKey key, Guid tenantId, Guid userId, DateTimeOffset issuedAtUtc, TimeSpan lifetime) =>
        $"access:{userId}";

    public string IssueIdToken(
        ActiveSigningKey key, Guid tenantId, Guid userId, string email, bool emailVerified, string username,
        DateTimeOffset issuedAtUtc, TimeSpan lifetime) =>
        $"id:{userId}";
}
