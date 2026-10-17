using FluentResults;
using MediatR;
using WardenId.Audit.Application.Abstractions;
using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Features.CheckpointKeys.Rotate;

/// <summary>
/// Rotates the checkpoint signing key and records it as an admin event (spec §6: "key rotation must itself
/// be an audited event chained into the log"). The key signs checkpoints for every tenant, so the event goes
/// to the platform channel with no tenant. Fails closed: without an ambient actor there's nobody to record,
/// so nothing rotates.
/// </summary>
public sealed class Handler(IAuditCheckpointKeyRotator rotator, IAuditContextAccessor auditContextAccessor)
    : IRequestHandler<Command, Result<RotatedCheckpointKey>>
{
    private const string ActiveKidProperty = "ActiveKid";

    public async Task<Result<RotatedCheckpointKey>> Handle(Command request, CancellationToken cancellationToken)
    {
        var ambient = auditContextAccessor.Current;
        if (ambient is null)
            return Result.Fail(AuditErrors.Validation("No audit context: the rotation can't be recorded, so it was not done."));

        var result = await rotator.RotateAsync((previousKid, newKid) => BuildEvent(ambient, previousKid, newKid), cancellationToken);

        return result.IsSuccess
            ? Result.Ok(new RotatedCheckpointKey(result.Value.Kid))
            : Result.Fail(result.Errors);
    }

    private static AuditEvent BuildEvent(AuditAmbientContext ambient, string? previousKid, string newKid) =>
        AuditEvent.Create(
            Guid.CreateVersion7(),
            AuditChannel.Platform,
            AuditEventType.AuditCheckpointKeyRotate,
            schemaVersion: 1,
            DateTimeOffset.UtcNow,
            AuditSeverity.Warning,
            ambient.Actor,
            AuditOutcome.Success(),
            ambient.Client,
            AuthContext.None(),
            ambient.Correlation,
            targets: [AuditTarget.Create(AuditTargetType.CheckpointSigningKey, newKid).Value],
            changes:
            [
                PropertyChange.Create(
                    ActiveKidProperty,
                    previousKid is null ? null : new AuditValue.Plain(previousKid),
                    new AuditValue.Plain(newKid)).Value
            ]).Value;
}
