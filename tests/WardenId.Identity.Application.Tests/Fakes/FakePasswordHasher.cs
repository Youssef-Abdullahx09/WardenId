namespace WardenId.Identity.Application.Tests.Fakes;

public sealed class FakePasswordHasher : IPasswordHasher
{
    public bool ForceNeedsRehash { get; set; }

    public string Hash(string plaintext) => $"hashed:{plaintext}";

    public bool Verify(string plaintext, string hash) => hash == Hash(plaintext);

    public bool NeedsRehash(string hash) => ForceNeedsRehash;
}
