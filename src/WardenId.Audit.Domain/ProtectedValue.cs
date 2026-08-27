using System.Security.Cryptography;
using System.Text;

namespace WardenId.Audit.Domain;

/// <summary>
/// The only way a sensitive value (authorization code, access/refresh/ID token, client
/// secret, password, PKCE verifier, session cookie) may enter an audit envelope — see
/// docs/audit/audit-module-spec.md I9. No member exposes the raw value; the raw string is
/// never retained past the constructor call that hashes it.
/// </summary>
public sealed record ProtectedValue
{
    private const int PrefixLength = 4;

    public string Hash { get; }
    public string? Prefix { get; }

    private ProtectedValue(string hash, string? prefix)
    {
        Hash = hash;
        Prefix = prefix;
    }

    public static ProtectedValue Redact(string rawValue, bool includePrefix = true)
    {
        if (string.IsNullOrEmpty(rawValue))
            throw new ArgumentException("Value to redact cannot be null or empty.", nameof(rawValue));

        var hash = ComputeHash(rawValue);
        var prefix = includePrefix && rawValue.Length > PrefixLength ? rawValue[..PrefixLength] : null;
        return new ProtectedValue(hash, prefix);
    }

    /// <summary>Rehydrates a value already reduced to hash/prefix — canonical deserialization only, never sees the raw value.</summary>
    public static ProtectedValue FromTrustedSource(string hash, string? prefix)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Hash cannot be null or empty.", nameof(hash));

        return new ProtectedValue(hash, prefix);
    }

    private static string ComputeHash(string rawValue)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawValue))).ToLowerInvariant();
}
