using WardenId.Audit.Infrastructure.Sealing;

namespace WardenId.Audit.Infrastructure.Persistence;

/// <summary>Table, index and filter names in the <see cref="AuditDbContext.Schema"/> schema.</summary>
public static class AuditDbNames
{
    public static class Tables
    {
        public const string Checkpoints = "checkpoints";
        public const string CheckpointSigningKeys = "checkpoint_signing_keys";
        public const string Segments = "segments";
    }

    public static class Indexes
    {
        public const string ActiveCheckpointSigningKeyUnique = "IX_CheckpointSigningKeys_ActiveUnique";
    }

    public static class Filters
    {
        public const string ActiveCheckpointSigningKey = $"\"{nameof(AuditCheckpointSigningKeyRecord.IsActive)}\" = true";
    }
}
