using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Audit.Infrastructure.Sealing;

namespace WardenId.Audit.Infrastructure.Persistence.Configurations;

public sealed class AuditCheckpointSigningKeyConfiguration : IEntityTypeConfiguration<AuditCheckpointSigningKeyRecord>
{
    public void Configure(EntityTypeBuilder<AuditCheckpointSigningKeyRecord> builder)
    {
        builder.ToTable("checkpoint_signing_keys");

        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();

        builder.Property(k => k.PublicKeyJwk).HasColumnType("jsonb").IsRequired();
        builder.Property(k => k.EncryptedPrivateKey).IsRequired();
        builder.Property(k => k.CreatedAt).IsRequired();

        // At most one active signing key at a time — enforced at the DB level, not just by
        // Es256CheckpointSigner's own read-then-write check, since multiple sealer replicas
        // could race to create the first key concurrently.
        builder.HasIndex(k => k.IsActive)
            .IsUnique()
            .HasFilter("\"IsActive\" = true")
            .HasDatabaseName("IX_CheckpointSigningKeys_ActiveUnique");
    }
}
