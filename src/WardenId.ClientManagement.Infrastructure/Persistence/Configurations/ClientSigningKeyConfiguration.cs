using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Infrastructure.Persistence.Configurations;

public sealed class ClientSigningKeyConfiguration : IEntityTypeConfiguration<ClientSigningKey>
{
    public void Configure(EntityTypeBuilder<ClientSigningKey> builder)
    {
        builder.ToTable(SchemaConsts.ClientSigningKeysTable);

        builder.Property(k => k.Kid).HasMaxLength(100).IsRequired();
        builder.Property(k => k.PublicKeyJwk).HasColumnType("jsonb").IsRequired();
        builder.Property(k => k.Algorithm).HasMaxLength(20).IsRequired();
        builder.Property(k => k.ExpiresAt);

        builder.HasIndex("ClientId").HasDatabaseName("IX_ClientSigningKeys_ClientId");
        builder.HasIndex("Kid").IsUnique().HasDatabaseName("IX_ClientSigningKeys_Kid_Unique");
    }
}
