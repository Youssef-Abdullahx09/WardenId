using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserAttributeConfiguration : IEntityTypeConfiguration<UserAttribute>
{
    public void Configure(EntityTypeBuilder<UserAttribute> builder)
    {
        builder.ToTable(SchemaConsts.UserAttributesTable);

        builder.Property(a => a.Key).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Value).IsRequired();
        builder.Property(a => a.ValueType).HasConversion<string>().HasMaxLength(20).IsRequired();
    }
}
