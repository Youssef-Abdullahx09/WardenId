namespace WardenId.Audit.Infrastructure.Witnessing;

/// <summary>
/// Bound from ConfigurationKeys.Witness.Section. Both optional (spec §8) — AddAuditInfrastructure
/// only registers the witness whose setting is present, and logs a startup warning if neither is.
/// </summary>
public sealed class AuditWitnessOptions
{
    public string? AppendOnlyFilePath { get; init; }
    public string? WebhookUrl { get; init; }
}
