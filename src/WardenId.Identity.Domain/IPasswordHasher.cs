namespace WardenId.Identity.Domain;

public interface IPasswordHasher
{
    string Hash(string plaintext);
    bool Verify(string plaintext, string hash);
    bool NeedsRehash(string hash);
}
