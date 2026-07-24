using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace WardenId.SharedKernel.Infrastructure;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public bool Enabled { get; init; } = true;

    [Required]
    [ConfigurationKeyName("OTEL_SERVICE_NAME")]
    public string ServiceName { get; init; } = string.Empty;

    [Required]
    [ConfigurationKeyName("OTEL_EXPORTER_OTLP_ENDPOINT")]
    public string OtlpEndpoint { get; init; } = string.Empty;

    [Required]
    [ConfigurationKeyName("OTEL_EXPORTER_OTLP_PROTOCOL")]
    public string OtlpProtocol { get; init; } = string.Empty;

    [Required]
    [ConfigurationKeyName("OTEL_RESOURCE_ATTRIBUTES")]
    public string ResourceAttributes { get; init; } = string.Empty;
}
