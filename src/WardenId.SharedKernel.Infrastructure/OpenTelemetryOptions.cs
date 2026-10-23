using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace WardenId.SharedKernel.Infrastructure;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public bool Enabled { get; init; } = true;

    [Required]
    [ConfigurationKeyName(OpenTelemetryEnvironmentVariables.ServiceName)]
    public string ServiceName { get; init; } = string.Empty;

    [Required]
    [ConfigurationKeyName(OpenTelemetryEnvironmentVariables.ExporterOtlpEndpoint)]
    public string OtlpEndpoint { get; init; } = string.Empty;

    [Required]
    [ConfigurationKeyName(OpenTelemetryEnvironmentVariables.ExporterOtlpProtocol)]
    public string OtlpProtocol { get; init; } = string.Empty;

    [Required]
    [ConfigurationKeyName(OpenTelemetryEnvironmentVariables.ResourceAttributes)]
    public string ResourceAttributes { get; init; } = string.Empty;
}
