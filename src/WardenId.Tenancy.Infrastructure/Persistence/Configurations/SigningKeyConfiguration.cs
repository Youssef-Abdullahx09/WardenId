using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.SharedKernel.Persistence;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Infrastructure.Persistence.Configurations;

public sealed class SigningKeyConfiguration: IEntityTypeConfiguration<SigningKey>
{
    public void Configure(EntityTypeBuilder<SigningKey> builder)
    {
        builder.ToTable(SchemaConsts.SigningKeysTable);

        builder.Property(k => k.Algorithm)
            .HasMaxLength(20)
            .IsRequired();
        
        builder.Property(k => k.PublicKeyJwk)
            .HasColumnType(PostgresConventions.ColumnTypes.Jsonb)
            .IsRequired();
 
        builder.Property(k => k.EncryptedPrivateKey)
            .IsRequired();

        builder.Property(k => k.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(k => k.ActivatedAt);
        builder.Property(k => k.RetiringSince);
        builder.Property(k => k.RetiredAt);

        // Computed from Status; not columns.
        builder.Ignore(k => k.IsActive);
        builder.Ignore(k => k.IsPublished);
        
        builder.HasIndex(SchemaConsts.SigningKeyTenantIdColumn)
            .HasDatabaseName(SchemaConsts.SigningKeysTenantIdIndex);
        
        builder.HasIndex(SchemaConsts.SigningKeyTenantIdColumn)
            .IsUnique()
            .HasFilter($"\"{nameof(SigningKey.Status)}\" = '{nameof(SigningKeyStatus.Active)}'")
            .HasDatabaseName(SchemaConsts.SigningKeysActiveUniqueIndex);
    }
}