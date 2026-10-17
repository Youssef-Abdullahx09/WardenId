using Microsoft.EntityFrameworkCore;
using WardenId.SharedKernel.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Audit.Infrastructure.Tests.Fakes;

/// <summary>A minimal stand-in for a real module's DbContext, wired the same way a real one would be if it opted into AuditCaptureInterceptor.</summary>
internal sealed class TestAuditableDbContext(DbContextOptions<TestAuditableDbContext> options) : DbContextBase(options)
{
    public DbSet<TestAggregate> TestAggregates => Set<TestAggregate>();
    public DbSet<BadTargetTypeAggregate> BadTargetTypeAggregates => Set<BadTargetTypeAggregate>();
    public DbSet<ParentAggregate> ParentAggregates => Set<ParentAggregate>();
    public DbSet<AuditEventRecord> AuditEvents => Set<AuditEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestAggregate>(builder => builder.Ignore(a => a.DomainEvents));
        modelBuilder.Entity<BadTargetTypeAggregate>(builder => builder.Ignore(a => a.DomainEvents));
        modelBuilder.Entity<ParentAggregate>(builder =>
        {
            builder.Ignore(a => a.DomainEvents);
            builder.HasMany(a => a.Keys).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(a => a.Keys).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.HasMany(a => a.Notes).WithOne().HasForeignKey("NoteOwnerId").OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(a => a.Notes).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.OwnsMany(a => a.Tags, tags =>
            {
                tags.WithOwner().HasForeignKey("OwnerId");
                tags.Property(t => t.Value);
                tags.HasKey("OwnerId", "Value");
            });
            builder.Navigation(a => a.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.OwnsOne(a => a.Limits, limits => limits.Property(l => l.Max));
        });
        modelBuilder.Entity<ChildKey>().Property(k => k.Id).ValueGeneratedNever();
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());

        base.OnModelCreating(modelBuilder);
    }

    public static TestAuditableDbContext CreateNew(string databaseName, AuditCaptureInterceptor interceptor) => new(
        new DbContextOptionsBuilder<TestAuditableDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(interceptor)
            .Options);
}
