namespace WardenId.Identity.Domain.Tests;

public class MfaPolicyTests
{
    [Fact]
    public void Create_SetsMode()
    {
        var policy = MfaPolicy.Create(MfaPolicyMode.Required);

        Assert.Equal(MfaPolicyMode.Required, policy.Mode);
    }

    [Fact]
    public void TwoPoliciesWithSameMode_AreEqual()
    {
        var a = MfaPolicy.Create(MfaPolicyMode.Optional);
        var b = MfaPolicy.Create(MfaPolicyMode.Optional);

        Assert.Equal(a, b);
    }
}
