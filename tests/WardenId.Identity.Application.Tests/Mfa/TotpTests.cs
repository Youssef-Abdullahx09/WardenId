using WardenId.Identity.Application.Mfa;

namespace WardenId.Identity.Application.Tests.Mfa;

public class TotpTests
{
    // RFC 6238 appendix B, HMAC-SHA1 rows (8 digits).
    [Theory]
    [InlineData(59L, "94287082")]
    [InlineData(1111111109L, "07081804")]
    [InlineData(1111111111L, "14050471")]
    [InlineData(1234567890L, "89005924")]
    [InlineData(2000000000L, "69279037")]
    [InlineData(20000000000L, "65353130")]
    public void ComputeCode_MatchesTheRfc6238Sha1Vectors(long unixSeconds, string expected)
    {
        var step = Totp.TimeStepAt(DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

        Assert.Equal(expected, Totp.ComputeCode(TestMfa.Secret, step, digits: 8));
    }

    [Fact]
    public void ComputeCode_HasSixDigitsByDefault_TheLastSixOfTheEightDigitValue()
    {
        var step = Totp.TimeStepAt(DateTimeOffset.FromUnixTimeSeconds(1111111109));

        Assert.Equal("081804", Totp.ComputeCode(TestMfa.Secret, step));
    }

    [Theory]
    [InlineData(-30)]
    [InlineData(0)]
    [InlineData(30)]
    public void MatchingTimeSteps_AcceptsTheCurrentStepAndOneEitherSide(int offsetSeconds)
    {
        var now = TestMfa.Now;
        var code = TestMfa.CodeAt(now.AddSeconds(offsetSeconds));

        var steps = Totp.MatchingTimeSteps(TestMfa.Secret, code, now);

        Assert.Equal([Totp.TimeStepAt(now.AddSeconds(offsetSeconds))], steps);
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(60)]
    public void MatchingTimeSteps_RefusesTwoStepsAway(int offsetSeconds)
    {
        var now = TestMfa.Now;

        Assert.Empty(Totp.MatchingTimeSteps(TestMfa.Secret, TestMfa.CodeAt(now.AddSeconds(offsetSeconds)), now));
    }

    [Fact]
    public void MatchingTimeSteps_IgnoresSpaces()
    {
        var code = TestMfa.CodeAt(TestMfa.Now);

        Assert.Single(Totp.MatchingTimeSteps(TestMfa.Secret, $" {code[..3]} {code[3..]} ", TestMfa.Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    public void NormalizeCode_RefusesAnythingButSixDigits(string code)
    {
        Assert.Null(Totp.NormalizeCode(code));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void ToBase32_MatchesRfc4648_WithoutPadding(string input, string expected)
    {
        Assert.Equal(expected, Totp.ToBase32(System.Text.Encoding.ASCII.GetBytes(input)));
    }

    [Fact]
    public void GenerateSecret_Is160Bits()
    {
        Assert.Equal(20, Totp.GenerateSecret().Length);
    }

    [Fact]
    public void OtpAuthUri_NamesTheIssuerAndAccount_AndTheParameters()
    {
        var uri = Totp.OtpAuthUri("Acme Corp", "alice@acme.test", "JBSWY3DPEHPK3PXP");

        Assert.Equal(
            "otpauth://totp/Acme%20Corp:alice%40acme.test?secret=JBSWY3DPEHPK3PXP&issuer=Acme%20Corp&algorithm=SHA1&digits=6&period=30",
            uri);
    }
}
