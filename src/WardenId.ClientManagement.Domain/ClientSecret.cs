using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class ClientSecret : Entity
{
    // EF Core materialization only
    private ClientSecret() { }

    private ClientSecret(string hashedValue, string label, DateTimeOffset? expiresAt)
    {
        HashedValue = hashedValue;
        Label = label;
        ExpiresAt = expiresAt;
    }

    public string HashedValue { get; private set; }
    public string Label { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsExpired(DateTimeOffset asOf) => ExpiresAt is not null && ExpiresAt <= asOf;

    public static ClientSecret Create(string hashedValue, string label, DateTimeOffset? expiresAt) =>
        new(hashedValue, label, expiresAt);

    public void SetExpiry(DateTimeOffset expiresAt) => ExpiresAt = expiresAt;
}
