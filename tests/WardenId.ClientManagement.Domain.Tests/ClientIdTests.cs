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

    [Theory]
    [InlineData("warden-console")]
    [InlineData("Admin_UI-01")]
    public void Create_AcceptsValuesInTheAlphabet(string raw)
    {
        var result = ClientId.Create(raw);

        Assert.True(result.IsSuccess);
        Assert.Equal(raw, result.Value.Value);
    }

    [Fact]
    public void Create_TrimsSurroundingWhitespace()
    {
        var result = ClientId.Create("  warden-console  ");

        Assert.Equal("warden-console", result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData("warden.console")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")]
    public void Create_FailsOnEmptyOrInvalidValues(string? raw)
    {
        var result = ClientId.Create(raw);

        Assert.True(result.IsFailed);
    }
}
