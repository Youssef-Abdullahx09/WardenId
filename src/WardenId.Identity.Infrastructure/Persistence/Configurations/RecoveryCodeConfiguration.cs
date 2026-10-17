using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Infrastructure.Persistence.Configurations;

public sealed class RecoveryCodeConfiguration : IEntityTypeConfiguration<RecoveryCode>
{
    public void Configure(EntityTypeBuilder<RecoveryCode> builder)
    {
        builder.ToTable(SchemaConsts.RecoveryCodesTable);

        builder.Property(c => c.CodeHash).IsRequired();
        builder.Property(c => c.UsedAtUtc);

        // Concurrency token: a uint row version is mapped by Npgsql to the system column "xmin" (no real column).
        // A save that updates or deletes this row fails if another request changed it first — see
        // UserRepository.SaveAsync. It's a shadow property: the domain has no use for it.
        builder.Property<uint>(SchemaConsts.RowVersion).IsRowVersion();
    }
}
