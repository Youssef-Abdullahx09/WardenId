using FluentResults;
using MediatR;

namespace WardenId.Audit.Application.Features.CheckpointKeys.List;

public sealed record Query : IRequest<Result<IReadOnlyList<CheckpointKeySummary>>>;

public sealed record CheckpointKeySummary(string Kid, string PublicKeyJwk, bool IsActive, DateTimeOffset CreatedAt);
