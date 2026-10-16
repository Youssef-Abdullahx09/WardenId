using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace WardenId.Gateway.Health;

/// <summary>The broker CAP publishes to, read from the same RabbitMq config section.</summary>
public sealed record RabbitMqHealthCheckSettings(string Host, int Port, string Username, string Password);

/// <summary>
/// Opens (and closes) an AMQP connection to the broker CAP uses, so wrong credentials count as a failure,
/// not just a closed port.
/// </summary>
public sealed class RabbitMqHealthCheck(RabbitMqHealthCheckSettings settings) : IHealthCheck
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            UserName = settings.Username,
            Password = settings.Password,
            RequestedConnectionTimeout = ConnectionTimeout,
            AutomaticRecoveryEnabled = false,
            ClientProvidedName = "WardenId health check"
        };

        try
        {
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, "RabbitMQ connection is not open.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: exception);
        }
    }
}
