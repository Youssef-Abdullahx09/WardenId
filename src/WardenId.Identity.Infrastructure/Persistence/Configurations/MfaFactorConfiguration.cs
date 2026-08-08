using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Infrastructure.Persistence.Configurations;

public sealed class MfaFactorConfiguration : IEntityTypeConfiguration<MfaFactor>
{
    public void Configure(EntityTypeBuilder<MfaFactor> builder)
    {
        builder.ToTable(SchemaConsts.MfaFactorsTable);

        builder.Property(f => f.FactorType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(f => f.CredentialData).IsRequired();
        builder.Property(f => f.Enabled).IsRequired();
        builder.Property(f => f.EnrolledAtUtc).IsRequired();
        builder.Property(f => f.VerifiedAtUtc);
    }
}
