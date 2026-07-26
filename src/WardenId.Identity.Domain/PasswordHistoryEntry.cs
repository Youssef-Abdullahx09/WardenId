using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class PasswordHistoryEntry : Entity
{
    // EF Core materialization only
    private PasswordHistoryEntry() { }

    private PasswordHistoryEntry(string hash, string algorithm, DateTimeOffset setAtUtc)
    {
        Hash = hash;
        Algorithm = algorithm;
        SetAtUtc = setAtUtc;
    }

    public string Hash { get; private set; }
    public string Algorithm { get; private set; }
    public DateTimeOffset SetAtUtc { get; private set; }

    public static PasswordHistoryEntry Create(string hash, string algorithm, DateTimeOffset setAtUtc) =>
        new(hash, algorithm, setAtUtc);
}
