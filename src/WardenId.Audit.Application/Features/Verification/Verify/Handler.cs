using System.Security.Cryptography;
using System.Text.Json;
using FluentResults;
using MediatR;
using WardenId.Audit.Application.Abstractions;
using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Features.Verification.Verify;

/// <summary>
/// Full verification per spec §7: (1) checkpoint signature (key-rotation continuity is a
/// documented non-check — see VerificationReport), (2) header chain, (3) per-segment root
/// recomputation, (4) sequence density + contiguity. Always returns Result.Ok — a failed
/// verification is a normal, successfully-computed report (Verified = false), not an HTTP
/// error; only "the channel/tenant scope couldn't be read at all" would be a failure, which
/// doesn't happen here since an empty scope just yields an empty (trivially verified) report.
/// </summary>
public sealed class Handler(IAuditProofRepository repository) : IRequestHandler<Query, Result<VerificationReport>>
{
    public async Task<Result<VerificationReport>> Handle(Query request, CancellationToken cancellationToken)
    {
        var segments = await repository.GetSegmentsAsync(request.Channel, request.TenantId, cancellationToken);
        var checkpoints = await repository.GetCheckpointsAsync(request.Channel, request.TenantId, cancellationToken);
        var keysByKid = (await repository.GetCheckpointKeysAsync(cancellationToken)).ToDictionary(k => k.Kid);

        var segmentResults = new List<SegmentVerification>();
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var previous = i == 0 ? null : segments[i - 1];

            var leafHashes = await repository.GetSegmentLeafHashesAsync(
                request.Channel, request.TenantId, segment.SequenceStart, segment.SequenceEnd, cancellationToken);
            var expectedCount = segment.SequenceEnd - segment.SequenceStart + 1;
            var dense = leafHashes.Count == expectedCount;

            var contiguous = previous is null ? segment.SequenceStart == 1 : segment.SequenceStart == previous.SequenceEnd + 1;

            var rootMatches = dense && MerkleTree.ComputeRoot(leafHashes).AsSpan().SequenceEqual(segment.MerkleRoot);

            var expectedPreviousHeaderHash = previous?.HeaderHash;
            var recomputedHeader = AuditSegment.Create(
                segment.Id, request.Channel, request.TenantId, segment.SequenceStart, segment.SequenceEnd,
                segment.MerkleRoot, expectedPreviousHeaderHash, segment.SealedAt);
            var chainLinked = ByteArraysEqual(segment.PreviousHeaderHash, expectedPreviousHeaderHash)
                && rootMatches
                && recomputedHeader.IsSuccess
                && recomputedHeader.Value.HeaderHash.AsSpan().SequenceEqual(segment.HeaderHash);

            segmentResults.Add(new SegmentVerification(
                segment.Id, segment.SequenceStart, segment.SequenceEnd, dense, contiguous, rootMatches, chainLinked));
        }

        var checkpointResults = new List<CheckpointVerification>();
        foreach (var checkpoint in checkpoints)
        {
            var covered = segments.Take((int)checkpoint.TreeSize).Select(s => s.HeaderHash).ToList();
            var rootMatches = covered.Count == checkpoint.TreeSize
                && MerkleTree.ComputeRoot(covered).AsSpan().SequenceEqual(checkpoint.RootHash);

            var signingKeyKnown = keysByKid.TryGetValue(checkpoint.Kid, out var key);
            var signatureValid = signingKeyKnown && VerifySignature(request.Channel, request.TenantId, checkpoint, key!);

            checkpointResults.Add(new CheckpointVerification(
                checkpoint.Id, checkpoint.TreeSize, rootMatches, signatureValid, signingKeyKnown));
        }

        return Result.Ok(new VerificationReport(request.Channel, request.TenantId, segmentResults, checkpointResults));
    }

    private static bool VerifySignature(AuditChannel channel, Guid? tenantId, CheckpointRow checkpoint, CheckpointKeyRow key)
    {
        // A tampered/malformed public key row (bad JWK JSON, a byte string that isn't a
        // valid curve point) must fail verification, not crash the whole report.
        try
        {
            using var jwk = JsonDocument.Parse(key.PublicKeyJwk);
            var x = Base64UrlDecode(jwk.RootElement.GetProperty("x").GetString()!);
            var y = Base64UrlDecode(jwk.RootElement.GetProperty("y").GetString()!);

            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = x, Y = y }
            });

            var scopeLabel = tenantId?.ToString() ?? "platform";
            var unsignedBody = AuditCheckpoint.BuildUnsignedBody(channel, scopeLabel, checkpoint.TreeSize, checkpoint.RootHash);

            return ecdsa.VerifyData(unsignedBody, checkpoint.Signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or FormatException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool ByteArraysEqual(byte[]? a, byte[]? b)
    {
        if (a is null || b is null) return a is null && b is null;
        return a.AsSpan().SequenceEqual(b);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
