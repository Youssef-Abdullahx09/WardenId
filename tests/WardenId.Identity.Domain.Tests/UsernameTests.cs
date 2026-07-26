namespace WardenId.Identity.Domain.Tests;

public class UsernameTests
{
    [Fact]
    public void Create_FailsOnEmpty()
    {
        var result = Username.Create("");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenTooShort()
    {
        var result = Username.Create("ab");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenTooLong()
    {
        var result = Username.Create(new string('a', 65));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnInvalidCharacters()
    {
        var result = Username.Create("bad username!");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SucceedsOnValidUsername()
    {
        var result = Username.Create("john.doe-99");

        Assert.True(result.IsSuccess);
        Assert.Equal("john.doe-99", result.Value.Value);
    }

    [Fact]
    public void FromTrustedSource_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => Username.FromTrustedSource(""));
    }
}
