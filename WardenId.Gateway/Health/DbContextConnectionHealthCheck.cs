using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WardenId.Gateway.Health;

/// <summary>
/// Can the module's DbContext open a connection to its database? Uses the context's own registration:
/// a pooled factory when the module registers one (Audit), otherwise the scoped context. Unlike the
/// EF Core health-check package, which only resolves a scoped context.
/// </summary>
public sealed class DbContextConnectionHealthCheck<TContext>(IServiceProvider services) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = services.GetService<IDbContextFactory<TContext>>();
            if (factory is not null)
            {
                await using var created = await factory.CreateDbContextAsync(cancellationToken);
                return await ProbeAsync(created, context, cancellationToken);
            }

            return await ProbeAsync(services.GetRequiredService<TContext>(), context, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: exception);
        }
    }

    private static async Task<HealthCheckResult> ProbeAsync(TContext dbContext, HealthCheckContext context, CancellationToken cancellationToken) =>
        await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, "Cannot connect to the database.");
}
