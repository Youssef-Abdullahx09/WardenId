namespace WardenId.Audit.Infrastructure.Exporting;

/// <summary>Bound from ConfigurationKeys.Exporter.Section.</summary>
public sealed class AuditExporterOptions
{
    public int PollIntervalSeconds { get; init; } = 60;

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);
}
