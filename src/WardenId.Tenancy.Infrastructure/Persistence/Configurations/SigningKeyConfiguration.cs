using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
            .HasColumnType("jsonb")
            .IsRequired();
 
        builder.Property(k => k.EncryptedPrivateKey)
            .IsRequired();
        
        builder.HasIndex("TenantId")
            .HasDatabaseName("IX_TenantSigningKeys_TenantId");
        
        builder.HasIndex("TenantId")
            .IsUnique()
            .HasFilter($"\"{nameof(SigningKey.IsActive)}\" = true")
            .HasDatabaseName("IX_TenantSigningKeys_TenantId_ActiveUnique");
    }
}