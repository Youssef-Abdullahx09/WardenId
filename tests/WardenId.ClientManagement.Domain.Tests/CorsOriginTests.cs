namespace WardenId.ClientManagement.Domain.Tests;

public class CorsOriginTests
{
    [Fact]
    public void Create_FailsOnEmpty()
    {
        var result = CorsOrigin.Create("");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnRelativeValue()
    {
        var result = CorsOrigin.Create("example.com");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenPathIncluded()
    {
        var result = CorsOrigin.Create("https://app.example.com/some/path");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SucceedsOnBareOrigin()
    {
        var result = CorsOrigin.Create("https://app.example.com");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://app.example.com", result.Value.Value);
    }
}
