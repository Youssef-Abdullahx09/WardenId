namespace WardenId.Identity.Application.Tests.Fakes;

public sealed class FakeBreachedPasswordChecker : IBreachedPasswordChecker
{
    public bool IsBreached { get; set; }

    public Task<bool> IsBreachedAsync(string plaintext) => Task.FromResult(IsBreached);
}
