namespace WardenId.Protocol.Application.Abstractions;

public interface IJwtIssuer
{
    string IssueAccessToken(ActiveSigningKey key, Guid tenantId, Guid userId, DateTimeOffset issuedAtUtc, TimeSpan lifetime);

    string IssueIdToken(
        ActiveSigningKey key, Guid tenantId, Guid userId, string email, bool emailVerified, string username,
        DateTimeOffset issuedAtUtc, TimeSpan lifetime);
}
