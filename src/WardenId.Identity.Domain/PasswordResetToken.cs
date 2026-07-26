using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

// Kept separate from EmailVerificationToken (not a shared base) — same shape today,
// but consumption side effects differ (reset also clears account lockout).
public sealed class PasswordResetToken : Entity
{
    // EF Core materialization only
    private PasswordResetToken() { }

    private PasswordResetToken(string tokenHash, DateTimeOffset expiresAtUtc)
    {
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string TokenHash { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public static PasswordResetToken Create(string tokenHash, DateTimeOffset expiresAtUtc) =>
        new(tokenHash, expiresAtUtc);

    public bool Matches(string tokenHash) => TokenHash == tokenHash;

    public Result Consume(DateTimeOffset nowUtc)
    {
        if (ConsumedAtUtc is not null) return Result.Fail(UserErrors.TokenAlreadyConsumed());
        if (ExpiresAtUtc <= nowUtc) return Result.Fail(UserErrors.TokenExpired());

        ConsumedAtUtc = nowUtc;
        return Result.Ok();
    }
}
