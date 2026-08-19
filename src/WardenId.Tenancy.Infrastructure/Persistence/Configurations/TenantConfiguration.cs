using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable(SchemaConsts.TenantsTable);

        builder.Property(t => t.Slug)
            .HasConversion(slug =>
                    slug.Value,
                value => Slug.FromTrustedSource(value))
            .HasMaxLength(Slug.MaxLength)
            .IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.SuspendedAtUtc);
        builder.Property(t => t.SuspendedReason).HasMaxLength(500);

        // Concurrency token — Admin API will have concurrent writers eventually.
        // uint + IsRowVersion() is auto-mapped by Npgsql's provider to the "xmin" system column
        // (no real column created) rather than a server-generated value we'd have to supply.
        builder.Property(t => t.Version).IsRowVersion();

        builder
            .HasMany(t => t.SigningKeys)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);

        // EF Core finds the _signingKeys backing field by naming convention;
        // this just makes the access mode explicit.
        builder.Navigation(t => t.SigningKeys)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Domain events are dispatched via the SaveChangesInterceptor, never persisted.
        builder.Ignore(t => t.DomainEvents);
    }
}