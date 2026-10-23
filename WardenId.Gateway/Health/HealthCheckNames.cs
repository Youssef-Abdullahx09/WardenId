namespace WardenId.Gateway.Health;

/// <summary>Check names reported by /health/ready.</summary>
public static class HealthCheckNames
{
    public const string PostgresTenancy = "postgres-tenancy";
    public const string PostgresIdentity = "postgres-identity";
    public const string PostgresClientManagement = "postgres-clientmanagement";
    public const string PostgresProtocol = "postgres-protocol";
    public const string PostgresOpenIddict = "postgres-openiddict";
    public const string PostgresAudit = "postgres-audit";
    public const string RabbitMq = "rabbitmq";
}
