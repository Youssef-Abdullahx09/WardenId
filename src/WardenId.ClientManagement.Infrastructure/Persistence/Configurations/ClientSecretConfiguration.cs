using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Infrastructure.Persistence.Configurations;

public sealed class ClientSecretConfiguration : IEntityTypeConfiguration<ClientSecret>
{
    public void Configure(EntityTypeBuilder<ClientSecret> builder)
    {
        builder.ToTable(SchemaConsts.ClientSecretsTable);

        builder.Property(s => s.HashedValue).IsRequired();
        builder.Property(s => s.Label).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ExpiresAt);

        builder.HasIndex("ClientId").HasDatabaseName("IX_ClientSecrets_ClientId");
    }
}
