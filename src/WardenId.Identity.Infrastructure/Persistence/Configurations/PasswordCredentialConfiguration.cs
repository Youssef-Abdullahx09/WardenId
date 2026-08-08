using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Infrastructure.Persistence.Configurations;

public sealed class PasswordCredentialConfiguration : IEntityTypeConfiguration<PasswordCredential>
{
    public void Configure(EntityTypeBuilder<PasswordCredential> builder)
    {
        builder.ToTable(SchemaConsts.PasswordCredentialsTable);

        builder.Property(c => c.Hash).IsRequired();
        builder.Property(c => c.Algorithm).HasMaxLength(50).IsRequired();
        builder.Property(c => c.AlgorithmVersion).IsRequired();
        builder.Property(c => c.SetAtUtc).IsRequired();
    }
}
