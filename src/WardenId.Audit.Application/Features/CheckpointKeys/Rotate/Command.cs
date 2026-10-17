using FluentResults;
using MediatR;

namespace WardenId.Audit.Application.Features.CheckpointKeys.Rotate;

public sealed record Command : IRequest<Result<RotatedCheckpointKey>>;

/// <param name="Kid">The key new checkpoints are signed with from now on.</param>
public sealed record RotatedCheckpointKey(string Kid);
