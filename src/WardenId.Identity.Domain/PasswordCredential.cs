using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class PasswordCredential : Entity
{
    // EF Core materialization only
    private PasswordCredential() { }

    private PasswordCredential(string hash, string algorithm, int algorithmVersion, DateTimeOffset setAtUtc)
    {
        Hash = hash;
        Algorithm = algorithm;
        AlgorithmVersion = algorithmVersion;
        SetAtUtc = setAtUtc;
    }

    public string Hash { get; private set; }
    public string Algorithm { get; private set; }
    public int AlgorithmVersion { get; private set; }
    public DateTimeOffset SetAtUtc { get; private set; }

    public static PasswordCredential Create(string hash, string algorithm, int algorithmVersion, DateTimeOffset setAtUtc) =>
        new(hash, algorithm, algorithmVersion, setAtUtc);

    public bool NeedsRehash(int currentAlgorithmVersion) => AlgorithmVersion < currentAlgorithmVersion;

    public void Rehash(string newHash, int newAlgorithmVersion, DateTimeOffset rehashedAtUtc)
    {
        Hash = newHash;
        AlgorithmVersion = newAlgorithmVersion;
        SetAtUtc = rehashedAtUtc;
    }
}
