using FluentResults;
using MediatR;
using WardenId.Audit.Application.Abstractions;

namespace WardenId.Audit.Application.Features.CheckpointKeys.List;

public sealed class Handler(IAuditProofRepository repository) : IRequestHandler<Query, Result<IReadOnlyList<CheckpointKeySummary>>>
{
    public async Task<Result<IReadOnlyList<CheckpointKeySummary>>> Handle(Query request, CancellationToken cancellationToken)
    {
        var keys = await repository.GetCheckpointKeysAsync(cancellationToken);
        IReadOnlyList<CheckpointKeySummary> summaries =
            keys.Select(k => new CheckpointKeySummary(k.Kid, k.PublicKeyJwk, k.IsActive, k.CreatedAt)).ToList();

        return Result.Ok(summaries);
    }
}
