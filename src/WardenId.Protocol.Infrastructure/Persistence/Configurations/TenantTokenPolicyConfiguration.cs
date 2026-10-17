using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Protocol.Domain.TokenPolicies;

namespace WardenId.Protocol.Infrastructure.Persistence.Configurations;

public sealed class TenantTokenPolicyConfiguration : IEntityTypeConfiguration<TenantTokenPolicy>
{
    public const string TenantTokenPoliciesTable = "TenantTokenPolicies";

    public void Configure(EntityTypeBuilder<TenantTokenPolicy> builder)
    {
        builder.ToTable(TenantTokenPoliciesTable);

        builder.Property(p => p.TenantId).IsRequired();
        // One policy per tenant; also the hot-path lookup (TenantLifetimes).
        builder.HasIndex(p => p.TenantId).IsUnique();

        // Nullable intervals: null = the global default applies.
        builder.Property(p => p.AccessTokenLifetime);
        builder.Property(p => p.IdTokenLifetime);
        builder.Property(p => p.RefreshTokenLifetime);
        builder.Property(p => p.RefreshTokenAbsoluteLifetime);
        builder.Property(p => p.AuthorizationCodeLifetime);
        builder.Property(p => p.SessionCookieLifetime);
        builder.Property(p => p.SessionAbsoluteLifetime);

        builder.Ignore(p => p.Overrides);
        builder.Ignore(p => p.DomainEvents);
    }
}
