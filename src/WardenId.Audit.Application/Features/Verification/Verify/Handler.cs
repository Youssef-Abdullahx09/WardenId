using System.Security.Cryptography;
using System.Text.Json;
using FluentResults;
using MediatR;
using WardenId.Audit.Application.Abstractions;
using WardenId.Audit.Domain;
using WardenId.Audit.Domain.Utilities;
using WardenId.SharedKernel.Domain.Utilities;

namespace WardenId.Audit.Application.Features.Verification.Verify;

/// <summary>
/// Full verification per spec §7: (1) checkpoint signature and key-rotation continuity
/// (see VerificationReport), (2) header chain, (3) per-segment root
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
        var keys = await repository.GetCheckpointKeysAsync(cancellationToken);
        var keysByKid = keys.ToDictionary(k => k.Kid);
        var continuousKids = ContinuousKids(keys);

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
                checkpoint.Id, checkpoint.TreeSize, rootMatches, signatureValid, signingKeyKnown,
                continuousKids.Contains(checkpoint.Kid), checkpoint.Kid));
        }

        return Result.Ok(new VerificationReport(request.Channel, request.TenantId, segmentResults, checkpointResults));
    }

    private static bool VerifySignature(AuditChannel channel, Guid? tenantId, CheckpointRow checkpoint, CheckpointKeyRow key)
    {
        var scopeLabel = tenantId?.ToString() ?? AuditFormats.Scopes.Platform;
        var unsignedBody = AuditCheckpoint.BuildUnsignedBody(channel, scopeLabel, checkpoint.TreeSize, checkpoint.RootHash);
        return VerifyEs256(key.PublicKeyJwk, unsignedBody, checkpoint.Signature);
    }

    /// <summary>
    /// The kids reachable from the first key through valid rotation statements. The first key (earliest
    /// created) is the root; any other key needs a PreviousKid that is itself continuous and whose public
    /// key verifies the statement naming this key's kid and public point.
    /// </summary>
    private static HashSet<string> ContinuousKids(IReadOnlyList<CheckpointKeyRow> keys)
    {
        var continuous = new HashSet<string>();
        var ordered = keys.OrderBy(k => k.CreatedAt).ToList();
        if (ordered.Count == 0) return continuous;

        if (ordered[0].PreviousKid is null) continuous.Add(ordered[0].Kid);

        var keysByKid = keys.ToDictionary(k => k.Kid);
        foreach (var key in ordered.Skip(1))
        {
            if (key.PreviousKid is null || key.RotationAttestation is null) continue;
            if (!continuous.Contains(key.PreviousKid) || !keysByKid.TryGetValue(key.PreviousKid, out var previous)) continue;
            if (!TryReadPoint(key.PublicKeyJwk, out var x, out var y)) continue;

            var statement = CheckpointKeyRotation.BuildUnsignedBody(previous.Kid, key.Kid, x, y);
            if (VerifyEs256(previous.PublicKeyJwk, statement, key.RotationAttestation)) continuous.Add(key.Kid);
        }

        return continuous;
    }

    private static bool TryReadPoint(string publicKeyJwk, out string x, out string y)
    {
        x = y = string.Empty;
        try
        {
            using var jwk = JsonDocument.Parse(publicKeyJwk);
            x = jwk.RootElement.GetProperty(JsonWebKeyValues.PublicMembers.X).GetString() ?? string.Empty;
            y = jwk.RootElement.GetProperty(JsonWebKeyValues.PublicMembers.Y).GetString() ?? string.Empty;
            return x.Length > 0 && y.Length > 0;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool VerifyEs256(string publicKeyJwk, byte[] data, byte[] signature)
    {
        // A tampered/malformed public key row (bad JWK JSON, a byte string that isn't a
        // valid curve point) must fail verification, not crash the whole report.
        try
        {
            if (!TryReadPoint(publicKeyJwk, out var x, out var y)) return false;

            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = Base64UrlDecode(x), Y = Base64UrlDecode(y) }
            });

            return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
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
