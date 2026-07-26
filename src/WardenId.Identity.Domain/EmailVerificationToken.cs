using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class EmailVerificationToken : Entity
{
    // EF Core materialization only
    private EmailVerificationToken() { }

    private EmailVerificationToken(string tokenHash, DateTimeOffset expiresAtUtc)
    {
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string TokenHash { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public static EmailVerificationToken Create(string tokenHash, DateTimeOffset expiresAtUtc) =>
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
