using FluentResults;

namespace WardenId.Identity.Domain;

public static class PasswordPolicyEvaluator
{
    public static async Task<Result> EvaluateAsync(
        PasswordPolicy policy,
        string candidatePassword,
        PasswordCredential? currentCredential,
        IReadOnlyCollection<PasswordHistoryEntry> history,
        IPasswordHasher passwordHasher,
        IBreachedPasswordChecker breachChecker)
    {
        if (candidatePassword.Length < policy.MinLength) return Result.Fail(UserErrors.PasswordTooShort(policy.MinLength));
        if (policy.RequireUppercase && !candidatePassword.Any(char.IsUpper)) return Result.Fail(UserErrors.PasswordMissingUppercase());
        if (policy.RequireDigit && !candidatePassword.Any(char.IsDigit)) return Result.Fail(UserErrors.PasswordMissingDigit());
        if (policy.RequireSymbol && candidatePassword.All(char.IsLetterOrDigit)) return Result.Fail(UserErrors.PasswordMissingSymbol());

        if (policy.BreachCheckEnabled && await breachChecker.IsBreachedAsync(candidatePassword))
            return Result.Fail(UserErrors.PasswordBreached());

        if (currentCredential is not null && passwordHasher.Verify(candidatePassword, currentCredential.Hash))
            return Result.Fail(UserErrors.PasswordReused());

        if (history.Any(entry => passwordHasher.Verify(candidatePassword, entry.Hash)))
            return Result.Fail(UserErrors.PasswordReused());

        return Result.Ok();
    }
}
