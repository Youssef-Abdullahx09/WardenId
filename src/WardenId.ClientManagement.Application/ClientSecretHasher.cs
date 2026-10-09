using System.Security.Cryptography;
using System.Text;

namespace WardenId.ClientManagement.Application;

/// <summary>
/// Client secrets are 32 random bytes, so an unsalted SHA-256 is sufficient (no dictionary to attack).
/// Matching is constant-time and always visits every candidate so timing doesn't reveal which one matched.
/// </summary>
public static class ClientSecretHasher
{
    public static string Hash(string plainText) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainText)));

    public static bool Matches(string presented, IEnumerable<string> hashedValues)
    {
        var presentedHash = Encoding.UTF8.GetBytes(Hash(presented));
        var matched = false;

        foreach (var hashedValue in hashedValues)
        {
            matched |= CryptographicOperations.FixedTimeEquals(presentedHash, Encoding.UTF8.GetBytes(hashedValue));
        }

        return matched;
    }
}
