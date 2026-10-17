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
        builder.Property(f => f.LastUsedAtUtc);
        builder.Property(f => f.LastAcceptedTimeStep);

        // Concurrency token: a uint row version is mapped by Npgsql to the system column "xmin" (no real column).
        // A save that updates or deletes this row fails if another request changed it first — see
        // UserRepository.SaveAsync. It's a shadow property: the domain has no use for it.
        builder.Property<uint>(SchemaConsts.RowVersion).IsRowVersion();
    }
}
