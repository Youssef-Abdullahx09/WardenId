using System.Security.Cryptography;
using System.Text;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Mfa;

/// <summary>
/// Recovery codes look like <c>7k3vq-x9m2c</c>: 10 characters of Crockford's base32 alphabet in lower case (no i, l,
/// o or u), so 50 bits each, in two groups of five. Only a SHA-256 hash of the normalized code is stored.
/// </summary>
public static class RecoveryCodes
{
    public const int Count = 10;
    private const int GroupLength = 5;
    private const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    public static (IReadOnlyList<string> Raw, IReadOnlyList<RecoveryCode> Codes) Generate()
    {
        var raw = new string[Count];
        var codes = new RecoveryCode[Count];
        for (var i = 0; i < Count; i++)
        {
            var value = RandomNumberGenerator.GetString(Alphabet, GroupLength * 2);
            raw[i] = $"{value[..GroupLength]}-{value[GroupLength..]}";
            codes[i] = RecoveryCode.Create(Hash(value));
        }

        return (raw, codes);
    }

    /// <summary>
    /// The code as stored before hashing: lower case, without dashes or whitespace, with the letters people mistake
    /// for digits (o, i, l) read as those digits. Null when it can't be a recovery code.
    /// </summary>
    public static string? Normalize(string? code)
    {
        if (code is null) return null;

        var builder = new StringBuilder(GroupLength * 2);
        foreach (var c in code)
        {
            if (c == '-' || char.IsWhiteSpace(c)) continue;
            var lower = char.ToLowerInvariant(c);
            lower = lower switch
            {
                'o' => '0',
                'i' or 'l' => '1',
                _ => lower
            };
            if (!Alphabet.Contains(lower)) return null;
            builder.Append(lower);
        }

        return builder.Length == GroupLength * 2 ? builder.ToString() : null;
    }

    public static string Hash(string normalizedCode) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedCode)));
}
