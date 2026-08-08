using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Infrastructure.Persistence.Configurations;

public sealed class PasswordHistoryEntryConfiguration : IEntityTypeConfiguration<PasswordHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PasswordHistoryEntry> builder)
    {
        builder.ToTable(SchemaConsts.PasswordHistoryEntriesTable);

        builder.Property(h => h.Hash).IsRequired();
        builder.Property(h => h.Algorithm).HasMaxLength(50).IsRequired();
        builder.Property(h => h.SetAtUtc).IsRequired();
    }
}
