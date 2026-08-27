namespace WardenId.Audit.Domain.Tests;

public class ClientContextTests
{
    [Fact]
    public void Create_WithValidIpAddress_Succeeds()
    {
        var result = ClientContext.Create("203.0.113.7");

        Assert.True(result.IsSuccess);
        Assert.Equal("203.0.113.7", result.Value.IpAddress);
    }

    [Fact]
    public void Create_WithNoIpAddress_Succeeds()
    {
        var result = ClientContext.Create(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.IpAddress);
    }

    [Fact]
    public void Create_WithInvalidIpAddress_Fails()
    {
        var result = ClientContext.Create("not-an-ip");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Unknown_HasAllFieldsNull()
    {
        var context = ClientContext.Unknown();

        Assert.Null(context.IpAddress);
        Assert.Null(context.UserAgent);
        Assert.Null(context.Asn);
        Assert.Null(context.GeoLocation);
    }
}
