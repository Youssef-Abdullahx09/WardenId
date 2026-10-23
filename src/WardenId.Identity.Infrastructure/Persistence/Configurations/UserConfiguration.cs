using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

        builder.Property(u => u.FirstName)
            .HasConversion(name => name!.Value, value => PersonName.FromTrustedSource(value))
            .HasMaxLength(PersonName.MaxLength);

        builder.Property(u => u.LastName)
            .HasConversion(name => name!.Value, value => PersonName.FromTrustedSource(value))
            .HasMaxLength(PersonName.MaxLength);

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.OwnsOne(u => u.Lockout, o =>
        {
            o.Property(l => l.FailedAttemptCount).HasColumnName(nameof(LoginLockoutState.FailedAttemptCount));
            o.Property(l => l.LockedUntilUtc).HasColumnName(nameof(LoginLockoutState.LockedUntilUtc));
        });

        builder.Property(u => u.PendingActions)
            .HasConversion(
                actions => actions.Select(a => a.ToString()).ToArray(),
                values => values.Select(Enum.Parse<RequiredAction>).ToList(),
                new ValueComparer<IReadOnlyCollection<RequiredAction>>(
                    (a, b) => (a ?? Array.Empty<RequiredAction>()).SequenceEqual(b ?? Array.Empty<RequiredAction>()),
                    a => a.Aggregate(0, (hash, action) => HashCode.Combine(hash, action)),
                    a => a.ToList()))
            .HasColumnName(nameof(User.PendingActions));

        builder.HasOne(u => u.Credential)
            .WithOne()
            .HasForeignKey<PasswordCredential>(SchemaConsts.UserForeignKey)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.MfaFactors)
            .WithOne()
            .HasForeignKey(SchemaConsts.UserForeignKey)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.MfaFactors).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(u => u.RecoveryCodes)
            .WithOne()
            .HasForeignKey(SchemaConsts.UserForeignKey)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.RecoveryCodes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(u => u.Attributes)
            .WithOne()
            .HasForeignKey(SchemaConsts.UserForeignKey)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.Attributes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(u => u.PasswordHistory)
            .WithOne()
            .HasForeignKey(SchemaConsts.UserForeignKey)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.PasswordHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(u => u.EmailVerificationTokens)
            .WithOne()
            .HasForeignKey(SchemaConsts.UserForeignKey)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.EmailVerificationTokens).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(u => u.PasswordResetTokens)
            .WithOne()
            .HasForeignKey(SchemaConsts.UserForeignKey)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.PasswordResetTokens).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(u => u.DomainEvents);

        // Concurrency token: a uint row version is mapped by Npgsql to the system column "xmin" (no real column).
        // A save that updates or deletes this row fails if another request changed it first — see
        // UserRepository.SaveAsync. It's a shadow property: the domain has no use for it.
        builder.Property<uint>(SchemaConsts.RowVersion).IsRowVersion();
    }
}
