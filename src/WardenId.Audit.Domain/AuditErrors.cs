using FluentResults;

namespace WardenId.Audit.Domain;

public static class AuditErrors
{
    public static Error NotFound =>
        new Error("Audit event not found.")
            .WithMetadata(AuditErrorCodes.MetadataKey, AuditErrorCodes.NotFound);

    public static Error Validation(string reason)
        => new Error(reason)
            .WithMetadata(AuditErrorCodes.MetadataKey, AuditErrorCodes.Validation);

    public static Error CheckpointKeyRotationConflict()
        => new Error("Another checkpoint key rotation finished first; nothing was changed. Retry if a new key is still wanted.")
            .WithMetadata(AuditErrorCodes.MetadataKey, AuditErrorCodes.Conflict);

    public static Error SchemaVersionMustBePositive()
        => Validation("Schema version must be a positive integer.");

    public static Error ActorIdRequired()
        => Validation("Actor id is required.");

    public static Error ClientContextInvalidIpAddress()
        => Validation("Client IP address is not a valid IP address.");

    public static Error TargetIdRequired()
        => Validation("Target id is required.");

    public static Error PropertyChangeNameRequired()
        => Validation("Property change name is required.");

    public static Error SegmentSequenceRangeInvalid()
        => Validation("Segment sequence end cannot be before sequence start.");

    public static Error SegmentMerkleRootRequired()
        => Validation("Segment Merkle root is required.");

    public static Error CheckpointTreeSizeMustBePositive()
        => Validation("Checkpoint tree size must be a positive integer.");

    public static Error CheckpointRootHashRequired()
        => Validation("Checkpoint root hash is required.");

    public static Error CheckpointKidRequired()
        => Validation("Checkpoint signing key id is required.");

    public static Error CheckpointSignatureRequired()
        => Validation("Checkpoint signature is required.");
}
