namespace WardenId.Identity.Domain;

public interface IBreachedPasswordChecker
{
    Task<bool> IsBreachedAsync(string plaintext);
}
