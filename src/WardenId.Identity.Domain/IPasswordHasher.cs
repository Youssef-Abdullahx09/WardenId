namespace WardenId.Identity.Domain;

public interface IPasswordHasher
{
    // Recorded on PasswordCredential/PasswordHistoryEntry as provenance — must match what Hash()
    // actually produces, so callers derive it from here rather than hardcoding a literal.
    string Algorithm { get; }

    string Hash(string plaintext);
    bool Verify(string plaintext, string hash);
    bool NeedsRehash(string hash);
}
