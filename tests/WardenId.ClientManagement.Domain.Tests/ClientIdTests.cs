namespace WardenId.ClientManagement.Domain.Tests;

public class ClientIdTests
{
    [Fact]
    public void Generate_ProducesA32CharacterValue()
    {
        var clientId = ClientId.Generate();

        Assert.Equal(32, clientId.Value.Length);
    }

    [Fact]
    public void Generate_ProducesDistinctValuesAcrossCalls()
    {
        var first = ClientId.Generate();
        var second = ClientId.Generate();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void FromTrustedSource_ThrowsOnNullOrEmpty()
    {
        Assert.Throws<ArgumentException>(() => ClientId.FromTrustedSource(""));
    }

    [Fact]
    public void FromTrustedSource_RoundTripsTheValue()
    {
        var clientId = ClientId.FromTrustedSource("abc123");

        Assert.Equal("abc123", clientId.Value);
    }
}
