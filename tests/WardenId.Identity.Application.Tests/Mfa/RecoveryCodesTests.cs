using System.Text.RegularExpressions;
using WardenId.Identity.Application.Mfa;

namespace WardenId.Identity.Application.Tests.Mfa;

public class RecoveryCodesTests
{
    [Fact]
    public void Generate_MakesTenDistinctCodes_StoredOnlyAsHashes()
    {
        var (raw, codes) = RecoveryCodes.Generate();

        Assert.Equal(10, raw.Count);
        Assert.Equal(10, raw.Distinct().Count());
        Assert.All(raw, code => Assert.Matches(new Regex("^[0-9a-hjkmnp-tv-z]{5}-[0-9a-hjkmnp-tv-z]{5}$"), code));
        for (var i = 0; i < raw.Count; i++)
        {
            Assert.DoesNotContain(raw[i].Replace("-", ""), codes[i].CodeHash, StringComparison.OrdinalIgnoreCase);
            Assert.True(codes[i].Matches(RecoveryCodes.Hash(RecoveryCodes.Normalize(raw[i])!)));
        }
    }

    [Theory]
    [InlineData("abcde-fgh23", "abcdefgh23")]
    [InlineData("ABCDE FGH23", "abcdefgh23")]
    [InlineData(" abcdefgh23 ", "abcdefgh23")]
    [InlineData("oOiIl-00111", "0011100111")]
    public void Normalize_IgnoresCaseDashesAndSpaces_AndReadsLookAlikesAsDigits(string input, string expected)
    {
        Assert.Equal(expected, RecoveryCodes.Normalize(input));
    }

    [Theory]
    [InlineData("abcde-fgh2")]
    [InlineData("abcde-fgh234")]
    [InlineData("abcde-fghu3")]
    [InlineData("123456")]
    public void Normalize_RefusesWhatCantBeARecoveryCode(string input)
    {
        Assert.Null(RecoveryCodes.Normalize(input));
    }
}
