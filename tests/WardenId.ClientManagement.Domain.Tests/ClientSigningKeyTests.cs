namespace WardenId.ClientManagement.Domain.Tests;

public class ClientSigningKeyTests
{
    [Fact]
    public void Create_SetsProvidedValues()
    {
        var key = ClientSigningKey.Create("kid-1", "{\"kty\":\"EC\"}", "ES256", expiresAt: null);

        Assert.Equal("kid-1", key.Kid);
        Assert.Equal("{\"kty\":\"EC\"}", key.PublicKeyJwk);
        Assert.Equal("ES256", key.Algorithm);
        Assert.Null(key.ExpiresAt);
    }
}
