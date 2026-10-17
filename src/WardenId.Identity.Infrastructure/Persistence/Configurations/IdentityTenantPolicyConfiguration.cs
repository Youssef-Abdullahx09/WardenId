using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Infrastructure.Persistence.Configurations;

public sealed class IdentityTenantPolicyConfiguration : IEntityTypeConfiguration<IdentityTenantPolicy>
{
    public void Configure(EntityTypeBuilder<IdentityTenantPolicy> builder)
    {
        builder.ToTable(SchemaConsts.TenantPoliciesTable);

        builder.Property(p => p.TenantId).IsRequired();
        builder.HasIndex(p => p.TenantId).IsUnique();

        // Every override column is nullable: null means "the global value".
        builder.Property(p => p.MfaMode)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Ignore(p => p.Overrides);
        builder.Ignore(p => p.DomainEvents);
    }
}
