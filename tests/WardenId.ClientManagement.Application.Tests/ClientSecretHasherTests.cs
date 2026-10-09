using System.Security.Cryptography;
using System.Text;

namespace WardenId.ClientManagement.Application.Tests;

public class ClientSecretHasherTests
{
    [Fact]
    public void Hash_IsUppercaseHexSha256_SameAsPreviousIssueAlgorithm() =>
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("abc"))),
            ClientSecretHasher.Hash("abc"));

    [Fact]
    public void Matches_IsTrue_WhenAnyHashMatches() =>
        Assert.True(ClientSecretHasher.Matches("b", [ClientSecretHasher.Hash("a"), ClientSecretHasher.Hash("b")]));

    [Fact]
    public void Matches_IsFalse_WhenNoHashMatches() =>
        Assert.False(ClientSecretHasher.Matches("c", [ClientSecretHasher.Hash("a"), ClientSecretHasher.Hash("b")]));

    [Fact]
    public void Matches_IsFalse_ForEmptyCandidateList() =>
        Assert.False(ClientSecretHasher.Matches("a", []));
}
