namespace WardenId.Identity.Domain.Tests;

public class PasswordPolicyEvaluatorTests
{
    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string plaintext) => "hashed:" + plaintext;
        public bool Verify(string plaintext, string hash) => hash == "hashed:" + plaintext;
        public bool NeedsRehash(string hash) => false;
    }

    private sealed class FakeBreachedPasswordChecker(bool isBreached) : IBreachedPasswordChecker
    {
        public Task<bool> IsBreachedAsync(string plaintext) => Task.FromResult(isBreached);
    }

    private static readonly PasswordPolicy Policy =
        PasswordPolicy.Create(8, requireUppercase: true, requireDigit: true, requireSymbol: false, breachCheckEnabled: true, historyCount: 2).Value;

    [Fact]
    public async Task EvaluateAsync_FailsWhenTooShort()
    {
        var result = await PasswordPolicyEvaluator.EvaluateAsync(
            Policy, "Ab1", [], new FakePasswordHasher(), new FakeBreachedPasswordChecker(false));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task EvaluateAsync_FailsWhenMissingUppercase()
    {
        var result = await PasswordPolicyEvaluator.EvaluateAsync(
            Policy, "lowercase1", [], new FakePasswordHasher(), new FakeBreachedPasswordChecker(false));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task EvaluateAsync_FailsWhenMissingDigit()
    {
        var result = await PasswordPolicyEvaluator.EvaluateAsync(
            Policy, "NoDigitsHere", [], new FakePasswordHasher(), new FakeBreachedPasswordChecker(false));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task EvaluateAsync_FailsWhenBreached()
    {
        var result = await PasswordPolicyEvaluator.EvaluateAsync(
            Policy, "Valid1Password", [], new FakePasswordHasher(), new FakeBreachedPasswordChecker(true));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task EvaluateAsync_FailsWhenReusedFromHistory()
    {
        var hasher = new FakePasswordHasher();
        var history = new[] { PasswordHistoryEntry.Create(hasher.Hash("Valid1Password"), "Argon2id", DateTimeOffset.UtcNow) };

        var result = await PasswordPolicyEvaluator.EvaluateAsync(
            Policy, "Valid1Password", history, hasher, new FakeBreachedPasswordChecker(false));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task EvaluateAsync_SucceedsWhenAllChecksPass()
    {
        var result = await PasswordPolicyEvaluator.EvaluateAsync(
            Policy, "Valid1Password", [], new FakePasswordHasher(), new FakeBreachedPasswordChecker(false));

        Assert.True(result.IsSuccess);
    }
}
