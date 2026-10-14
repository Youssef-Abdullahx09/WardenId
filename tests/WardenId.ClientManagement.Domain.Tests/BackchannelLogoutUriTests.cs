namespace WardenId.ClientManagement.Domain.Tests;

public class BackchannelLogoutUriTests
{
    [Fact]
    public void Create_SucceedsOnHttpsUri()
    {
        var result = BackchannelLogoutUri.Create("https://app.example.com/bcl");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://app.example.com/bcl", result.Value.Value);
    }

    [Theory]
    [InlineData("http://localhost:5000/bcl")]
    [InlineData("http://127.0.0.1/bcl")]
    [InlineData("http://[::1]:8080/bcl")]
    public void Create_SucceedsOnHttpLoopbackUri(string raw)
    {
        var result = BackchannelLogoutUri.Create(raw);

        Assert.True(result.IsSuccess);
        Assert.Equal(raw, result.Value.Value);
    }

    [Fact]
    public void Create_FailsOnHttpNonLoopbackUri_WithMustBeHttps()
    {
        var result = BackchannelLogoutUri.Create("http://app.example.com/bcl");

        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrors.BackchannelLogoutUriMustBeHttps().Message, result.Errors[0].Message);
        Assert.Equal(ClientErrorCodes.Validation, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);
    }

    // On Unix "/bcl" parses as an absolute file:// URI, so the error may be "must be https" rather than "invalid".
    // Either way it must fail.
    [Fact]
    public void Create_FailsOnRelativeUri()
    {
        var result = BackchannelLogoutUri.Create("/bcl");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnFragment_WithInvalid()
    {
        var result = BackchannelLogoutUri.Create("https://app.example.com/bcl#frag");

        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrors.BackchannelLogoutUriInvalid().Message, result.Errors[0].Message);
    }

    [Fact]
    public void Create_FailsOnUserInfo()
    {
        var result = BackchannelLogoutUri.Create("https://user:pass@app.example.com/bcl");

        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrors.BackchannelLogoutUriInvalid().Message, result.Errors[0].Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_FailsOnEmpty_WithInvalid(string? raw)
    {
        var result = BackchannelLogoutUri.Create(raw);

        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrors.BackchannelLogoutUriInvalid().Message, result.Errors[0].Message);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var result = BackchannelLogoutUri.Create("  https://app.example.com/bcl  ");

        Assert.Equal("https://app.example.com/bcl", result.Value.Value);
    }

    [Fact]
    public void TwoInstancesWithSameValue_AreEqual()
    {
        Assert.Equal(
            BackchannelLogoutUri.Create("https://app.example.com/bcl").Value,
            BackchannelLogoutUri.FromTrustedSource("https://app.example.com/bcl"));
    }
}
