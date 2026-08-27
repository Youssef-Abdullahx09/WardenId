using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WardenId.Audit.Infrastructure.Persistence.Configurations;

public sealed class AuditCheckpointConfiguration : IEntityTypeConfiguration<AuditCheckpointRecord>
{
    public void Configure(EntityTypeBuilder<AuditCheckpointRecord> builder)
    {
        builder.ToTable("checkpoints");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Channel).HasMaxLength(30).IsRequired();
        builder.Property(c => c.TreeSize).IsRequired();
        builder.Property(c => c.RootHash).IsRequired();
        builder.Property(c => c.Kid).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Signature).IsRequired();
        builder.Property(c => c.SignedAt).IsRequired();

        builder.HasOne<AuditSegmentRecord>().WithMany().HasForeignKey(c => c.SegmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.Channel, c.SignedAt });
    }
}
