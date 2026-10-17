using FluentResults;
using MediatR;

namespace WardenId.Audit.Application.Features.CheckpointKeys.List;

public sealed record Query : IRequest<Result<IReadOnlyList<CheckpointKeySummary>>>;

/// <param name="PreviousKid">The key this one replaced; null for the first key.</param>
/// <param name="RotationAttestation">
/// The previous key's ES256 signature (P1363, base64 in JSON) over the rotation statement
/// "warden-audit-key-rotation\n{previousKid}\n{kid}\n{x}\n{y}\n" — x and y from this key's JWK.
/// </param>
/// <param name="DeactivatedAt">When a rotation replaced this key; null while active.</param>
public sealed record CheckpointKeySummary(
    string Kid,
    string PublicKeyJwk,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? PreviousKid,
    byte[]? RotationAttestation,
    DateTimeOffset? DeactivatedAt);
