namespace WardenId.Identity.Domain.Tests;

public class PersonNameTests
{
    [Fact]
    public void Create_FailsOnBlank()
    {
        var result = PersonName.Create("  ", "First name");

        Assert.True(result.IsFailed);
        Assert.Equal("First name is required.", result.Errors[0].Message);
    }

    [Fact]
    public void Create_FailsWhenTooLong()
    {
        var result = PersonName.Create(new string('a', PersonName.MaxLength + 1), "Last name");

        Assert.True(result.IsFailed);
        Assert.Equal($"Last name cannot exceed {PersonName.MaxLength} characters.", result.Errors[0].Message);
    }

    [Fact]
    public void Create_TrimsAndKeepsCaseAndAnyCharacters()
    {
        var result = PersonName.Create("  Zoë O'Brien-Núñez ", "First name");

        Assert.True(result.IsSuccess);
        Assert.Equal("Zoë O'Brien-Núñez", result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateOptional_IsNull_ForBlank(string? raw)
    {
        var result = PersonName.CreateOptional(raw, "First name");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void CreateOptional_Validates_WhenSet()
    {
        Assert.Equal("Ada", PersonName.CreateOptional(" Ada ", "First name").Value!.Value);
        Assert.True(PersonName.CreateOptional(new string('a', PersonName.MaxLength + 1), "First name").IsFailed);
    }

    [Fact]
    public void FromTrustedSource_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => PersonName.FromTrustedSource(""));
    }
}
