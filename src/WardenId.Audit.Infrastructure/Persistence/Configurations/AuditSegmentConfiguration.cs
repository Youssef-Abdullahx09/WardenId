using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WardenId.Audit.Infrastructure.Persistence.Configurations;

public sealed class AuditSegmentConfiguration : IEntityTypeConfiguration<AuditSegmentRecord>
{
    public void Configure(EntityTypeBuilder<AuditSegmentRecord> builder)
    {
        builder.ToTable(AuditDbNames.Tables.Segments);

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Channel).HasMaxLength(30).IsRequired();
        builder.Property(s => s.SequenceStart).IsRequired();
        builder.Property(s => s.SequenceEnd).IsRequired();
        builder.Property(s => s.MerkleRoot).IsRequired();
        builder.Property(s => s.HeaderHash).IsRequired();
        builder.Property(s => s.SealedAt).IsRequired();
        builder.Property(s => s.ExportedAt);

        // "What's the last sealed segment for this channel" — the sealer's chaining lookup.
        builder.HasIndex(s => new { s.Channel, s.SequenceEnd });

        // "What's left to export" (exporter) / "what's safe to purge" (retention) both scan by this.
        builder.HasIndex(s => s.ExportedAt);
    }
}
