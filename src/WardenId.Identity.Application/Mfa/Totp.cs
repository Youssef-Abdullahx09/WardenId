using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace WardenId.Identity.Application.Mfa;

/// <summary>
/// TOTP (RFC 6238) with the parameters every authenticator app supports: HMAC-SHA1, 6 digits, a 30-second step.
/// A code is accepted for the current step and one step either side, for clock drift.
/// </summary>
public static class Totp
{
    public const int SecretLengthBytes = 20;
    public const int Digits = 6;
    public const int StepSeconds = 30;
    public const int AllowedDriftSteps = 1;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Parts of the Key URI format (otpauth://) that authenticator apps read.</summary>
    private static class KeyUri
    {
        public const string Prefix = "otpauth://totp/";
        public const string SecretParameter = "secret";
        public const string IssuerParameter = "issuer";
        public const string AlgorithmParameter = "algorithm";
        public const string DigitsParameter = "digits";
        public const string PeriodParameter = "period";
        public const string Sha1Algorithm = "SHA1";
    }

    /// <summary>A new random 160-bit shared secret.</summary>
    public static byte[] GenerateSecret() => RandomNumberGenerator.GetBytes(SecretLengthBytes);

    public static long TimeStepAt(DateTimeOffset time) => time.ToUnixTimeSeconds() / StepSeconds;

    /// <summary>The HOTP value (RFC 4226) of <paramref name="key"/> at <paramref name="timeStep"/>, zero-padded to <paramref name="digits"/>.</summary>
    public static string ComputeCode(byte[] key, long timeStep, int digits = Digits)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, timeStep);

        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(key, counter, hash);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];

        var modulus = (int)Math.Pow(10, digits);
        return (binary % modulus).ToString(new string('0', digits));
    }

    /// <summary>
    /// The time steps around <paramref name="now"/> (oldest first) whose code is <paramref name="code"/>. Empty when
    /// the code is malformed or matches none of them.
    /// </summary>
    public static IReadOnlyList<long> MatchingTimeSteps(byte[] key, string code, DateTimeOffset now)
    {
        var normalized = NormalizeCode(code);
        if (normalized is null) return [];

        var expected = Encoding.ASCII.GetBytes(normalized);
        var current = TimeStepAt(now);
        var matches = new List<long>(1);
        for (var step = current - AllowedDriftSteps; step <= current + AllowedDriftSteps; step++)
        {
            var candidate = Encoding.ASCII.GetBytes(ComputeCode(key, step));
            if (CryptographicOperations.FixedTimeEquals(candidate, expected)) matches.Add(step);
        }

        return matches;
    }

    /// <summary>The code with spaces removed, or null unless that leaves exactly <see cref="Digits"/> ASCII digits.</summary>
    public static string? NormalizeCode(string? code)
    {
        if (code is null) return null;

        var normalized = string.Concat(code.Where(c => !char.IsWhiteSpace(c)));
        return normalized.Length == Digits && normalized.All(char.IsAsciiDigit) ? normalized : null;
    }

    /// <summary>RFC 4648 base32, upper case, no padding — the form authenticator apps take.</summary>
    public static string ToBase32(ReadOnlySpan<byte> data)
    {
        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                builder.Append(Base32Alphabet[(buffer >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }

        if (bits > 0) builder.Append(Base32Alphabet[(buffer << (5 - bits)) & 0x1F]);
        return builder.ToString();
    }

    /// <summary>
    /// The Key URI an authenticator app reads from a QR code:
    /// <c>otpauth://totp/{issuer}:{account}?secret=…&amp;issuer=…&amp;algorithm=SHA1&amp;digits=6&amp;period=30</c>.
    /// Every part is percent-encoded as UTF-8. The label's only separator is the colon between issuer and account
    /// (apps split on it after decoding), so colons inside either label part become spaces; the <c>issuer</c>
    /// parameter, which apps prefer, keeps the name as it is.
    /// </summary>
    public static string OtpAuthUri(string issuer, string accountName, string base32Secret) =>
        $"{KeyUri.Prefix}{Uri.EscapeDataString(LabelPart(issuer))}:{Uri.EscapeDataString(LabelPart(accountName))}" +
        $"?{KeyUri.SecretParameter}={base32Secret}&{KeyUri.IssuerParameter}={Uri.EscapeDataString(issuer)}" +
        $"&{KeyUri.AlgorithmParameter}={KeyUri.Sha1Algorithm}&{KeyUri.DigitsParameter}={Digits}&{KeyUri.PeriodParameter}={StepSeconds}";

    private static string LabelPart(string value) =>
        string.Join(' ', value.Split(':', ' ').Where(part => part.Length > 0));
}
