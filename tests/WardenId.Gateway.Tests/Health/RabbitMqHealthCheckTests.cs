using Microsoft.Extensions.Diagnostics.HealthChecks;
using WardenId.Gateway.Health;

namespace WardenId.Gateway.Tests.Health;

public class RabbitMqHealthCheckTests
{
    [Fact]
    public async Task ReportsFailureStatus_WhenTheBrokerIsUnreachable()
    {
        var check = new RabbitMqHealthCheck(new RabbitMqHealthCheckSettings("127.0.0.1", 1, "guest", "guest"));
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("rabbitmq", check, HealthStatus.Degraded, [HealthCheckTags.Ready])
        };

        var result = await check.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }
}
