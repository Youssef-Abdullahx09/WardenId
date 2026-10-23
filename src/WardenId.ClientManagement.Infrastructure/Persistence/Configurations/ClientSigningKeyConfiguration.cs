using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.ClientManagement.Domain;
using WardenId.SharedKernel.Persistence.Utilities;

namespace WardenId.ClientManagement.Infrastructure.Persistence.Configurations;

public sealed class ClientSigningKeyConfiguration : IEntityTypeConfiguration<ClientSigningKey>
{
    public void Configure(EntityTypeBuilder<ClientSigningKey> builder)
    {
        builder.ToTable(SchemaConsts.ClientSigningKeysTable);

        builder.Property(k => k.Kid).HasMaxLength(100).IsRequired();
        builder.Property(k => k.PublicKeyJwk).HasColumnType(PostgresConventions.ColumnTypes.Jsonb).IsRequired();
        builder.Property(k => k.Algorithm).HasMaxLength(20).IsRequired();
        builder.Property(k => k.ExpiresAt);

        builder.HasIndex(SchemaConsts.ClientIdForeignKey).HasDatabaseName(SchemaConsts.Indexes.ClientSigningKeysClientId);
        builder.HasIndex(nameof(ClientSigningKey.Kid)).IsUnique().HasDatabaseName(SchemaConsts.Indexes.ClientSigningKeysKidUnique);
    }
}
