using WardenId.SharedKernel.Domain;

namespace WardenId.Protocol.Domain.Sessions;

/// <summary>
/// One OpenIddict ad-hoc authorization granted under the session (one per /authorize sign-in).
/// It is both a back-channel logout participant and what session end revokes; the authorization id
/// survives refresh-token rotation, so revoking it kills the whole token family.
/// </summary>
public sealed class SessionAuthorization : ValueObject
{
    // EF binds this constructor by parameter name.
    private SessionAuthorization(string authorizationId, string clientId, bool offlineAccess, DateTimeOffset createdAt)
    {
        AuthorizationId = authorizationId;
        ClientId = clientId;
        OfflineAccess = offlineAccess;
        CreatedAt = createdAt;
    }

    public string AuthorizationId { get; private set; }
    public string ClientId { get; private set; }
    public bool OfflineAccess { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static SessionAuthorization Create(
        string authorizationId,
        string clientId,
        bool offlineAccess,
        DateTimeOffset createdAt) =>
        new(authorizationId, clientId, offlineAccess, createdAt);

    internal SessionAuthorizationSnapshot ToSnapshot() => new(AuthorizationId, ClientId, OfflineAccess);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AuthorizationId;
        yield return ClientId;
        yield return OfflineAccess;
        yield return CreatedAt;
    }
}
