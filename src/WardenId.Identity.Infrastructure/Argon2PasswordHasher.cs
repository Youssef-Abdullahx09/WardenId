using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Isopoh.Cryptography.Argon2;
using WardenId.Identity.Domain;
using WardenId.Identity.Domain.Utilities;

namespace WardenId.Identity.Infrastructure;

public sealed partial class Argon2PasswordHasher : IPasswordHasher
{
    private const int TimeCost = 2;
    private const int MemoryCostKib = 19456;
    private const int Parallelism = 1;
    private const int SaltLengthBytes = 16;
    private const int HashLengthBytes = 32;

    public string Algorithm => PasswordHashAlgorithms.Argon2id;

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

        var memoryCost = int.Parse(match.Groups[MemoryCostGroup].Value);
        var timeCost = int.Parse(match.Groups[TimeCostGroup].Value);
        var parallelism = int.Parse(match.Groups[ParallelismGroup].Value);

        return memoryCost != MemoryCostKib || timeCost != TimeCost || parallelism != Parallelism;
    }

    // PHC string cost parameters, e.g. "m=19456,t=2,p=1".
    private const string MemoryCostGroup = "m";
    private const string TimeCostGroup = "t";
    private const string ParallelismGroup = "p";
    private const string CostParametersPattern =
        $@"m=(?<{MemoryCostGroup}>\d+),t=(?<{TimeCostGroup}>\d+),p=(?<{ParallelismGroup}>\d+)";

    [GeneratedRegex(CostParametersPattern)]
    private static partial Regex CostParametersRegex();
}
