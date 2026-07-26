namespace WardenId.Identity.Domain.Tests;

public class PasswordCredentialTests
{
    [Fact]
    public void Create_SetsAllFields()
    {
        var setAt = DateTimeOffset.UtcNow;

        var credential = PasswordCredential.Create("hash-v1", "Argon2id", 1, setAt);

        Assert.Equal("hash-v1", credential.Hash);
        Assert.Equal("Argon2id", credential.Algorithm);
        Assert.Equal(1, credential.AlgorithmVersion);
        Assert.Equal(setAt, credential.SetAtUtc);
    }

    [Fact]
    public void NeedsRehash_TrueWhenStoredVersionIsOlder()
    {
        var credential = PasswordCredential.Create("hash-v1", "Argon2id", 1, DateTimeOffset.UtcNow);

        Assert.True(credential.NeedsRehash(2));
    }

    [Fact]
    public void NeedsRehash_FalseWhenStoredVersionIsCurrent()
    {
        var credential = PasswordCredential.Create("hash-v1", "Argon2id", 2, DateTimeOffset.UtcNow);

        Assert.False(credential.NeedsRehash(2));
    }

    [Fact]
    public void Rehash_ReplacesHashAndVersionAndTimestamp()
    {
        var credential = PasswordCredential.Create("hash-v1", "Argon2id", 1, DateTimeOffset.UtcNow.AddDays(-1));
        var rehashedAt = DateTimeOffset.UtcNow;

        credential.Rehash("hash-v2", 2, rehashedAt);

        Assert.Equal("hash-v2", credential.Hash);
        Assert.Equal(2, credential.AlgorithmVersion);
        Assert.Equal(rehashedAt, credential.SetAtUtc);
    }

    [Fact]
    public void PasswordHistoryEntry_Create_SetsAllFields()
    {
        var setAt = DateTimeOffset.UtcNow;

        var entry = PasswordHistoryEntry.Create("hash-old", "Argon2id", setAt);

        Assert.Equal("hash-old", entry.Hash);
        Assert.Equal("Argon2id", entry.Algorithm);
        Assert.Equal(setAt, entry.SetAtUtc);
    }
}
