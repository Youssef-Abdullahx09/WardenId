using WardenId.Protocol.Application.Features.Grants;

namespace WardenId.Protocol.Application.Tests.Fakes;

public static class GrantBuilder
{
    public static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    public static StoredGrant Grant(
        Guid tenantId,
        Guid userId,
        string clientId = "web",
        GrantStatus status = GrantStatus.Valid,
        DateTimeOffset? createdAt = null,
        params string[] scopes) =>
        new(
            Guid.NewGuid().ToString(),
            userId.ToString(),
            new GrantOwner(tenantId, clientId),
            createdAt ?? Now,
            scopes.Length > 0 ? scopes : ["openid"],
            status);

    /// <summary>An authorization created before tenant/client stamping: its tenant can't be proven.</summary>
    public static StoredGrant Unstamped(Guid userId) =>
        new(Guid.NewGuid().ToString(), userId.ToString(), Owner: null, Now, ["openid"], GrantStatus.Valid);
}
