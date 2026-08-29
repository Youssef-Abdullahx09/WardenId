using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Protocol.Domain;

namespace WardenId.Protocol.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable(SchemaConsts.RefreshTokensTable);

        builder.Property(r => r.TokenHash)
            .HasMaxLength(64) // SHA-256 hex digest length
            .IsRequired();

        builder.HasIndex(r => r.TokenHash).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.UserId });

        builder.Ignore(r => r.DomainEvents);
    }
}
