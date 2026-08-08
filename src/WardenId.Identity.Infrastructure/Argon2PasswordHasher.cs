using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Isopoh.Cryptography.Argon2;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Infrastructure;

public sealed partial class Argon2PasswordHasher : IPasswordHasher
{
    private const int TimeCost = 2;
    private const int MemoryCostKib = 19456;
    private const int Parallelism = 1;
    private const int SaltLengthBytes = 16;
    private const int HashLengthBytes = 32;

    public string Algorithm => "Argon2id";

    public string Hash(string plaintext)
    {
        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing,
            Version = Argon2Version.Nineteen,
            TimeCost = TimeCost,
            MemoryCost = MemoryCostKib,
            Lanes = Parallelism,
            Threads = Parallelism,
            Password = Encoding.UTF8.GetBytes(plaintext),
            Salt = RandomNumberGenerator.GetBytes(SaltLengthBytes),
            HashLength = HashLengthBytes
        };

        using var argon2 = new Argon2(config);
        using var hash = argon2.Hash();
        return config.EncodeString(hash.Buffer);
    }

    public bool Verify(string plaintext, string hash)
    {
        try
        {
            return Argon2.Verify(hash, plaintext, Parallelism);
        }
        catch (Exception)
        {
            // Malformed/foreign-format hash — treat as a non-match rather than surfacing an error.
            return false;
        }
    }

    public bool NeedsRehash(string hash)
    {
        var match = CostParametersRegex().Match(hash);
        if (!match.Success) return true;

        var memoryCost = int.Parse(match.Groups["m"].Value);
        var timeCost = int.Parse(match.Groups["t"].Value);
        var parallelism = int.Parse(match.Groups["p"].Value);

        return memoryCost != MemoryCostKib || timeCost != TimeCost || parallelism != Parallelism;
    }

    [GeneratedRegex(@"m=(?<m>\d+),t=(?<t>\d+),p=(?<p>\d+)")]
    private static partial Regex CostParametersRegex();
}
