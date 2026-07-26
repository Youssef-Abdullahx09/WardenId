using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class PasswordPolicy : ValueObject
{
    public int MinLength { get; }
    public bool RequireUppercase { get; }
    public bool RequireDigit { get; }
    public bool RequireSymbol { get; }
    public bool BreachCheckEnabled { get; }
    public int HistoryCount { get; }

    private PasswordPolicy(
        int minLength, bool requireUppercase, bool requireDigit, bool requireSymbol,
        bool breachCheckEnabled, int historyCount)
    {
        MinLength = minLength;
        RequireUppercase = requireUppercase;
        RequireDigit = requireDigit;
        RequireSymbol = requireSymbol;
        BreachCheckEnabled = breachCheckEnabled;
        HistoryCount = historyCount;
    }

    public static Result<PasswordPolicy> Create(
        int minLength, bool requireUppercase, bool requireDigit, bool requireSymbol,
        bool breachCheckEnabled, int historyCount)
    {
        if (minLength < 1) return Result.Fail(UserErrors.PasswordPolicyMinLengthInvalid());
        if (historyCount < 0) return Result.Fail(UserErrors.PasswordPolicyHistoryCountInvalid());

        return Result.Ok(new PasswordPolicy(minLength, requireUppercase, requireDigit, requireSymbol, breachCheckEnabled, historyCount));
    }

    public static PasswordPolicy FromTrustedSource(
        int minLength, bool requireUppercase, bool requireDigit, bool requireSymbol,
        bool breachCheckEnabled, int historyCount)
        => new(minLength, requireUppercase, requireDigit, requireSymbol, breachCheckEnabled, historyCount);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MinLength;
        yield return RequireUppercase;
        yield return RequireDigit;
        yield return RequireSymbol;
        yield return BreachCheckEnabled;
        yield return HistoryCount;
    }
}
