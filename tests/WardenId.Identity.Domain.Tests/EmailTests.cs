namespace WardenId.Identity.Domain.Tests;

public class EmailTests
{
    [Fact]
    public void Create_FailsOnEmpty()
    {
        var result = Email.Create("");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnInvalidFormat()
    {
        var result = Email.Create("not-an-email");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenTooLong()
    {
        var result = Email.Create(new string('a', 250) + "@example.com");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_LowercasesAndTrimsValidEmail()
    {
        var result = Email.Create("  Someone@Example.COM  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("someone@example.com", result.Value.Value);
    }

    [Fact]
    public void FromTrustedSource_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => Email.FromTrustedSource(""));
    }

    [Fact]
    public void TwoEmailsWithSameValue_AreEqual()
    {
        var a = Email.Create("someone@example.com").Value;
        var b = Email.Create("someone@example.com").Value;

        Assert.Equal(a, b);
    }
}
