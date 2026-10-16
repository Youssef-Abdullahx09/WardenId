using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using WardenId.Gateway.Health;

namespace WardenId.Gateway.Tests.Health;

public class HealthCheckRegistrationTests
{
    [Fact]
    public void AddGatewayHealthChecks_RegistersAReadyCheckPerModuleDatabase_AndRabbitMq()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:Host"] = "localhost",
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddGatewayHealthChecks(configuration);

        var registrations = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Equal(
            new[]
            {
                "postgres-audit", "postgres-clientmanagement", "postgres-identity", "postgres-openiddict",
                "postgres-protocol", "postgres-tenancy", "rabbitmq"
            },
            registrations.Select(r => r.Name).Order().ToArray());
        Assert.All(registrations, r => Assert.Contains(HealthCheckTags.Ready, r.Tags));
        Assert.All(registrations.Where(r => r.Name.StartsWith("postgres-")), r => Assert.Equal(HealthStatus.Unhealthy, r.FailureStatus));
        // CAP's outbox keeps messages in Postgres while the broker is down, so a broker outage degrades, not unreadies.
        Assert.Equal(HealthStatus.Degraded, registrations.Single(r => r.Name == "rabbitmq").FailureStatus);
    }
}
