using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Infrastructure.Persistence.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable(SchemaConsts.ClientsTable);

        builder.Property(c => c.TenantId).IsRequired();
        builder.HasIndex(c => c.TenantId);

        builder.Property(c => c.ClientId)
            .HasConversion(
                clientId => clientId.Value,
                value => ClientId.FromTrustedSource(value))
            .HasMaxLength(ClientId.Length)
            .IsRequired();
        builder.HasIndex(c => new { c.TenantId, c.ClientId }).IsUnique();

        builder.Property(c => c.ClientType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.ClientAuthenticationMethod)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.GrantType)
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.Property(c => c.Enabled).IsRequired();
        builder.Property(c => c.IsFirstParty).IsRequired();
        builder.Property(c => c.RequirePkce).IsRequired();
        builder.Property(c => c.ConsentRequired).IsRequired();

        builder.Property(c => c.BackchannelLogoutUri)
            .HasConversion(
                uri => uri!.Value,
                value => BackchannelLogoutUri.FromTrustedSource(value))
            .HasMaxLength(2000);

        builder.OwnsOne(c => c.TokenLifetimeOverrides, o =>
        {
            o.Property(t => t.AccessTokenSeconds).HasColumnName("AccessTokenSeconds");
            o.Property(t => t.IdTokenSeconds).HasColumnName("IdTokenSeconds");
            o.Property(t => t.RefreshTokenSeconds).HasColumnName("RefreshTokenSeconds");
        });

        builder.OwnsMany(c => c.RedirectUris, b =>
        {
            b.ToTable(SchemaConsts.ClientRedirectUrisTable);
            b.WithOwner().HasForeignKey("ClientId");
            b.Property(r => r.Value).HasColumnName("Value").HasMaxLength(2000).IsRequired();
            b.HasKey("ClientId", "Value");
        });
        builder.Navigation(c => c.RedirectUris).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(c => c.PostLogoutRedirectUris, b =>
        {
            b.ToTable(SchemaConsts.ClientPostLogoutRedirectUrisTable);
            b.WithOwner().HasForeignKey("ClientId");
            b.Property(r => r.Value).HasColumnName("Value").HasMaxLength(2000).IsRequired();
            b.HasKey("ClientId", "Value");
        });
        builder.Navigation(c => c.PostLogoutRedirectUris).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(c => c.CorsOrigins, b =>
        {
            b.ToTable(SchemaConsts.ClientCorsOriginsTable);
            b.WithOwner().HasForeignKey("ClientId");
            b.Property(o => o.Value).HasColumnName("Value").HasMaxLength(255).IsRequired();
            b.HasKey("ClientId", "Value");
        });
        builder.Navigation(c => c.CorsOrigins).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(c => c.AssignedScopeIds)
            .HasColumnName("AssignedScopeIds");

        builder.HasMany(c => c.Secrets)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Secrets).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => c.SigningKeys)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.SigningKeys).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.DomainEvents);
    }
}
