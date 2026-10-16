using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WardenId.Audit.Infrastructure.Persistence;
using WardenId.Gateway.Health;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Infrastructure.Persistence;

namespace WardenId.Gateway.Tests.Health;

public class DbContextConnectionHealthCheckTests
{
    private static HealthCheckContext ContextFor(IHealthCheck check) => new()
    {
        Registration = new HealthCheckRegistration("db", check, HealthStatus.Unhealthy, [HealthCheckTags.Ready])
    };

    private static async Task<HealthCheckResult> RunAsync<TContext>(IServiceCollection services) where TContext : DbContext
    {
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var check = new DbContextConnectionHealthCheck<TContext>(scope.ServiceProvider);
        return await check.CheckHealthAsync(ContextFor(check));
    }

    [Fact]
    public async Task Healthy_WhenAScopedContextCanConnect()
    {
        var services = new ServiceCollection();
        // Registered the way the modules do: service interface + implementation.
        services.AddDbContext<ITenancyDbContext, TenancyDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var result = await RunAsync<TenancyDbContext>(services);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Healthy_WhenAPooledFactoryContextCanConnect()
    {
        var services = new ServiceCollection();
        // Audit registers only a pooled factory, no scoped context.
        services.AddPooledDbContextFactory<AuditDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var result = await RunAsync<AuditDbContext>(services);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ReportsFailureStatus_WhenPostgresIsUnreachable()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ITenancyDbContext, TenancyDbContext>(o =>
            o.UseNpgsql("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2"));

        var result = await RunAsync<TenancyDbContext>(services);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
