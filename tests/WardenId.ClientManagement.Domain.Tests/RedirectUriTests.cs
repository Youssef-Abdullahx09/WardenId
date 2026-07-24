namespace WardenId.ClientManagement.Domain.Tests;

public class RedirectUriTests
{
    [Fact]
    public void Create_FailsOnEmpty()
    {
        var result = RedirectUri.Create("");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnRelativeUri()
    {
        var result = RedirectUri.Create("/callback");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnFragment()
    {
        var result = RedirectUri.Create("https://app.example.com/callback#token");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnWildcard()
    {
        var result = RedirectUri.Create("https://*.example.com/callback");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SucceedsOnValidAbsoluteUri()
    {
        var result = RedirectUri.Create("https://app.example.com/callback");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://app.example.com/callback", result.Value.Value);
    }

    [Fact]
    public void TwoInstancesWithSameValue_AreEqual()
    {
        var first = RedirectUri.Create("https://app.example.com/callback").Value;
        var second = RedirectUri.Create("https://app.example.com/callback").Value;

        Assert.Equal(first, second);
    }
}
