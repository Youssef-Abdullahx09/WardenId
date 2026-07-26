namespace WardenId.Identity.Domain.Tests;

public class UserAttributeTests
{
    [Fact]
    public void Create_FailsOnEmptyKey()
    {
        var result = UserAttribute.Create("", "value", UserAttributeValueType.String);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SucceedsWithTrimmedKey()
    {
        var result = UserAttribute.Create("  department  ", "engineering", UserAttributeValueType.String);

        Assert.True(result.IsSuccess);
        Assert.Equal("department", result.Value.Key);
        Assert.Equal("engineering", result.Value.Value);
    }

    [Fact]
    public void UpdateValue_ReplacesValueAndType()
    {
        var attribute = UserAttribute.Create("age", "30", UserAttributeValueType.Number).Value;

        attribute.UpdateValue("31", UserAttributeValueType.Number);

        Assert.Equal("31", attribute.Value);
    }
}
