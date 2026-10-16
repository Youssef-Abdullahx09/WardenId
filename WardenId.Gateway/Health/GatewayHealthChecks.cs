using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WardenId.Audit.Infrastructure.Persistence;
using WardenId.ClientManagement.Infrastructure.Persistence;
using WardenId.Identity.Infrastructure.Persistence;
using WardenId.Protocol.Infrastructure.OpenIddict;
using WardenId.Protocol.Infrastructure.Persistence;
using WardenId.Tenancy.Infrastructure.Persistence;
using RabbitMqKeys = WardenId.SharedKernel.Infrastructure.ConfigurationKeys.RabbitMq;

namespace WardenId.Gateway.Health;

/// <summary>
/// Operational probes (FR-OB-04), outside /api and the OpenAPI document, anonymous:
/// <list type="bullet">
/// <item><c>/health/live</c> — the process is up and serving requests. Runs no checks.</item>
/// <item><c>/health/ready</c> — every module's database answers (503 when one doesn't), and the
/// RabbitMQ broker CAP publishes to (Degraded only: CAP's Postgres outbox holds messages while the
/// broker is down, so the API keeps working).</item>
/// </list>
/// Responses carry each check's name and status, never exception text.
/// </summary>
public static class GatewayHealthChecks
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    // Inside the probe's own timeout: a hung database or broker reports a failure rather than a hung probe.
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IHealthChecksBuilder AddGatewayHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var rabbitMq = configuration.GetSection(RabbitMqKeys.Section);
        var rabbitMqSettings = new RabbitMqHealthCheckSettings(
            rabbitMq[RabbitMqKeys.Host] ?? "localhost",
            rabbitMq.GetValue<int?>(RabbitMqKeys.Port) ?? 5672,
            rabbitMq[RabbitMqKeys.Username] ?? string.Empty,
            rabbitMq[RabbitMqKeys.Password] ?? string.Empty);

        return services.AddHealthChecks()
            .AddDatabase<TenancyDbContext>("postgres-tenancy")
            .AddDatabase<IdentityDbContext>("postgres-identity")
            .AddDatabase<ClientManagementDbContext>("postgres-clientmanagement")
            .AddDatabase<ProtocolDbContext>("postgres-protocol")
            .AddDatabase<OpenIddictDbContext>("postgres-openiddict")
            .AddDatabase<AuditDbContext>("postgres-audit")
            .AddCheck(
                "rabbitmq",
                new RabbitMqHealthCheck(rabbitMqSettings),
                HealthStatus.Degraded,
                [HealthCheckTags.Ready],
                CheckTimeout);
    }

    public static void MapGatewayHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks(LivePath, new HealthCheckOptions
            {
                Predicate = _ => false,
                ResponseWriter = WriteResponseAsync
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapHealthChecks(ReadyPath, new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
                ResponseWriter = WriteResponseAsync
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
    }

    private static IHealthChecksBuilder AddDatabase<TContext>(this IHealthChecksBuilder builder, string name)
        where TContext : DbContext =>
        builder.AddCheck<DbContextConnectionHealthCheck<TContext>>(
            name, HealthStatus.Unhealthy, [HealthCheckTags.Ready], CheckTimeout);

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds)
            })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
