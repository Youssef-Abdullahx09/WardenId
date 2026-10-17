using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Tests.Fakes;

public sealed class TestIdentityDbContext(DbContextOptions<TestIdentityDbContext> options)
    : DbContext(options), IIdentityDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<IdentityTenantPolicy> TenantPolicies => Set<IdentityTenantPolicy>();

    public Task<int> SaveChangesAsync(Guid userId, CancellationToken cancellationToken) =>
        SaveChangesAsync(cancellationToken);

    public Task BeginTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CommitTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollbackTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityTenantPolicy>().Ignore(p => p.DomainEvents);

        modelBuilder.Entity<User>(builder =>
        {
            builder.Ignore(u => u.DomainEvents);

            builder.Property(u => u.Email)
                .HasConversion(e => e.Value, v => Email.FromTrustedSource(v));
            builder.Property(u => u.Username)
                .HasConversion(u2 => u2.Value, v => Username.FromTrustedSource(v));

            builder.OwnsOne(u => u.Lockout, lockout =>
            {
                lockout.Property(l => l.FailedAttemptCount);
                lockout.Property(l => l.LockedUntilUtc);
            });

            builder.OwnsOne(u => u.Credential, credential =>
            {
                credential.Property(c => c.Hash);
                credential.Property(c => c.Algorithm);
                credential.Property(c => c.AlgorithmVersion);
                credential.Property(c => c.SetAtUtc);
            });

            builder.HasMany(u => u.MfaFactors).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(u => u.MfaFactors).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(u => u.RecoveryCodes).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(u => u.RecoveryCodes).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(u => u.Attributes).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(u => u.Attributes).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(u => u.PasswordHistory).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(u => u.PasswordHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(u => u.EmailVerificationTokens).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(u => u.EmailVerificationTokens).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(u => u.PasswordResetTokens).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(u => u.PasswordResetTokens).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.PrimitiveCollection(u => u.PendingActions)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    public static TestIdentityDbContext CreateNew(string databaseName) =>
        new(new DbContextOptionsBuilder<TestIdentityDbContext>().UseInMemoryDatabase(databaseName).Options);
}
