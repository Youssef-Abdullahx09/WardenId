using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(SchemaConsts.UsersTable);

        builder.Property(u => u.TenantId).IsRequired();
        builder.HasIndex(u => u.TenantId);

        builder.Property(u => u.Email)
            .HasConversion(email => email.Value, value => Email.FromTrustedSource(value))
            .HasMaxLength(Email.MaxLength)
            .IsRequired();
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();

        builder.Property(u => u.Username)
            .HasConversion(username => username.Value, value => Username.FromTrustedSource(value))
            .HasMaxLength(Username.MaxLength)
            .IsRequired();
        builder.HasIndex(u => new { u.TenantId, u.Username }).IsUnique();

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.OwnsOne(u => u.Lockout, o =>
        {
            o.Property(l => l.FailedAttemptCount).HasColumnName("FailedAttemptCount");
            o.Property(l => l.LockedUntilUtc).HasColumnName("LockedUntilUtc");
        });

        builder.Property(u => u.PendingActions)
            .HasConversion(
                actions => actions.Select(a => (int)a).ToArray(),
                values => values.Select(v => (RequiredAction)v).ToList())
            .HasColumnName("PendingActions");

        builder.HasOne(u => u.Credential)
            .WithOne()
            .HasForeignKey<PasswordCredential>("UserId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.MfaFactors)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.MfaFactors).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(u => u.RecoveryCodes)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.RecoveryCodes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(u => u.Attributes)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.Attributes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(u => u.PasswordHistory)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.PasswordHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(u => u.EmailVerificationTokens)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.EmailVerificationTokens).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(u => u.PasswordResetTokens)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.PasswordResetTokens).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(u => u.DomainEvents);
    }
}
