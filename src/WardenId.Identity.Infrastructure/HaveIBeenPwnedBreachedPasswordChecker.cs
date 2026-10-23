using System.Security.Cryptography;
using System.Text;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Infrastructure;

public sealed class HaveIBeenPwnedBreachedPasswordChecker(
    HttpClient httpClient) : IBreachedPasswordChecker
{
    private const string RangePath = "range/";

    public async Task<bool> IsBreachedAsync(string plaintext)
    {
        var sha1Hex = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(plaintext)));
        var prefix = sha1Hex[..5];
        var suffix = sha1Hex[5..];

        // k-Anonymity model: only the first 5 hex chars of the SHA-1 hash ever leave the process.
        var body = await httpClient.GetStringAsync(RangePath + prefix);

        foreach (var line in body.Split('\n'))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0) continue;

            var candidateSuffix = line[..separatorIndex].Trim();
            if (string.Equals(candidateSuffix, suffix, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
