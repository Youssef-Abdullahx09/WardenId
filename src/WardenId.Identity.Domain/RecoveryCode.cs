using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

/// <summary>A single-use code that stands in for a second factor. Only a hash of the code is stored.</summary>
public sealed class RecoveryCode : Entity
{
    // EF Core materialization only
    private RecoveryCode() { }

    private RecoveryCode(string codeHash) => CodeHash = codeHash;

    public string CodeHash { get; private set; }
    public DateTimeOffset? UsedAtUtc { get; private set; }

    public bool IsUsed => UsedAtUtc is not null;

    public static RecoveryCode Create(string codeHash) => new(codeHash);

    public bool Matches(string codeHash) => CodeHash == codeHash;

    public Result Consume(DateTimeOffset nowUtc)
    {
        if (UsedAtUtc is not null) return Result.Fail(UserErrors.RecoveryCodeAlreadyUsed());

        UsedAtUtc = nowUtc;
        return Result.Ok();
    }
}
