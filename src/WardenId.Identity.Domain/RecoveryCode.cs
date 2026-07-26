using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class RecoveryCode : Entity
{
    // EF Core materialization only
    private RecoveryCode() { }

    private RecoveryCode(string codeHash) => CodeHash = codeHash;

    public string CodeHash { get; private set; }
    public DateTimeOffset? UsedAtUtc { get; private set; }

    public static RecoveryCode Create(string codeHash) => new(codeHash);

    public bool Matches(string codeHash) => CodeHash == codeHash;

    public Result Consume()
    {
        if (UsedAtUtc is not null) return Result.Fail(UserErrors.RecoveryCodeAlreadyUsed());

        UsedAtUtc = DateTimeOffset.UtcNow;
        return Result.Ok();
    }
}
