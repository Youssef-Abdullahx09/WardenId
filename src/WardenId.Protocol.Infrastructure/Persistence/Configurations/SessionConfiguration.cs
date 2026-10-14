using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Infrastructure.Persistence.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public const string SessionsTable = "Sessions";
    public const string SessionAuthorizationsTable = "SessionAuthorizations";

    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable(SessionsTable);

        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.SubjectId).IsRequired();
        builder.Property(s => s.AuthTime).IsRequired();
        builder.Property(s => s.ExpiresAt).IsRequired();
        builder.Property(s => s.EndedAt);
        builder.Property(s => s.EndReason)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(s => s.TenantId);
        // EndForUser: a user's not-yet-ended sessions across tenants.
        builder.HasIndex(s => new { s.SubjectId, s.EndedAt });

        builder.OwnsMany(s => s.Authorizations, b =>
        {
            b.ToTable(SessionAuthorizationsTable);
            b.WithOwner().HasForeignKey("SessionId");
            b.Property(a => a.AuthorizationId).HasMaxLength(100).IsRequired();
            b.Property(a => a.ClientId).HasMaxLength(100).IsRequired();
            b.Property(a => a.OfflineAccess).IsRequired();
            b.Property(a => a.CreatedAt).IsRequired();
            b.HasKey("SessionId", nameof(SessionAuthorization.AuthorizationId));
        });
        builder.Navigation(s => s.Authorizations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(s => s.DomainEvents);
    }
}
