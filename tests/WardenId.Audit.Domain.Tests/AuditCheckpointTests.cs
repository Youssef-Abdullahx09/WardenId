using System.Text;

namespace WardenId.Audit.Domain.Tests;

public class AuditCheckpointTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveTreeSize_Fails(long treeSize)
    {
        var result = AuditCheckpoint.Create(
            Guid.NewGuid(), AuditChannel.Platform, null, treeSize, [1], "kid-1", [1], DateTimeOffset.UtcNow);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_WithEmptyRootHash_Fails()
    {
        var result = AuditCheckpoint.Create(
            Guid.NewGuid(), AuditChannel.Platform, null, 1, [], "kid-1", [1], DateTimeOffset.UtcNow);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_WithEmptySignature_Fails()
    {
        var result = AuditCheckpoint.Create(
            Guid.NewGuid(), AuditChannel.Platform, null, 1, [1], "kid-1", [], DateTimeOffset.UtcNow);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void BuildUnsignedBody_IsDeterministic()
    {
        var first = AuditCheckpoint.BuildUnsignedBody(AuditChannel.Platform, "platform", 10, [1, 2, 3]);
        var second = AuditCheckpoint.BuildUnsignedBody(AuditChannel.Platform, "platform", 10, [1, 2, 3]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void BuildUnsignedBody_DiffersWhenRootHashDiffers()
    {
        var first = AuditCheckpoint.BuildUnsignedBody(AuditChannel.Platform, "platform", 10, [1, 2, 3]);
        var second = AuditCheckpoint.BuildUnsignedBody(AuditChannel.Platform, "platform", 10, [4, 5, 6]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ToSignedNoteText_MatchesSpecFormat()
    {
        var rootHash = new byte[] { 1, 2, 3 };
        var checkpoint = AuditCheckpoint.Create(
            Guid.NewGuid(), AuditChannel.AdminConfig, tenantId: null, treeSize: 42, rootHash,
            kid: "key-1", signature: [9, 9], DateTimeOffset.UtcNow).Value;

        var text = checkpoint.ToSignedNoteText("platform");

        var expectedRootBase64 = Convert.ToBase64String(rootHash);
        var expectedSignatureBase64 = Convert.ToBase64String([9, 9]);
        Assert.Equal(
            $"warden-audit/platform/adminconfig\n42\n{expectedRootBase64}\n\n— key-1 {expectedSignatureBase64}\n",
            text);
    }

    [Fact]
    public void BuildUnsignedBody_IsAPrefixOf_ToSignedNoteText()
    {
        // The signer signs exactly the unsigned body — the signed note is that body plus the
        // appended signature line, never a re-derived or differently-formatted header.
        var rootHash = new byte[] { 7, 8, 9 };
        var unsignedBody = AuditCheckpoint.BuildUnsignedBody(AuditChannel.TokenOps, "platform", 5, rootHash);
        var checkpoint = AuditCheckpoint.Create(
            Guid.NewGuid(), AuditChannel.TokenOps, null, 5, rootHash, "kid-1", [1], DateTimeOffset.UtcNow).Value;

        var noteText = checkpoint.ToSignedNoteText("platform");

        Assert.StartsWith(Encoding.UTF8.GetString(unsignedBody), noteText);
    }
}
