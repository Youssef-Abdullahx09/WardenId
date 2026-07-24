namespace WardenId.ClientManagement.Domain.Tests;

public class ClientSecretTests
{
    [Fact]
    public void Create_SetsProvidedValues()
    {
        var secret = ClientSecret.Create("hashed", "primary", expiresAt: null);

        Assert.Equal("hashed", secret.HashedValue);
        Assert.Equal("primary", secret.Label);
        Assert.Null(secret.ExpiresAt);
    }

    [Fact]
    public void IsExpired_FalseWhenNoExpiry()
    {
        var secret = ClientSecret.Create("hashed", "primary", expiresAt: null);

        Assert.False(secret.IsExpired(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsExpired_TrueWhenPastExpiry()
    {
        var secret = ClientSecret.Create("hashed", "primary", DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.True(secret.IsExpired(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SetExpiry_UpdatesExpiresAt()
    {
        var secret = ClientSecret.Create("hashed", "primary", expiresAt: null);
        var expiry = DateTimeOffset.UtcNow.AddDays(1);

        secret.SetExpiry(expiry);

        Assert.Equal(expiry, secret.ExpiresAt);
    }
}
