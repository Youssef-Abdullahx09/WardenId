using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class ClientSigningKey : Entity
{
    // EF Core materialization only
    private ClientSigningKey() { }

    private ClientSigningKey(string kid, string publicKeyJwk, string algorithm, DateTimeOffset? expiresAt)
    {
        Kid = kid;
        PublicKeyJwk = publicKeyJwk;
        Algorithm = algorithm;
        ExpiresAt = expiresAt;
    }

    public string Kid { get; private set; }
    public string PublicKeyJwk { get; private set; }
    public string Algorithm { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    public static ClientSigningKey Create(
        string kid, string publicKeyJwk, string algorithm, DateTimeOffset? expiresAt) =>
        new(kid, publicKeyJwk, algorithm, expiresAt);
}
