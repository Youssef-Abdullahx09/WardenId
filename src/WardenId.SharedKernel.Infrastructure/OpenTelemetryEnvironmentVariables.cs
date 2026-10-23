namespace WardenId.SharedKernel.Infrastructure;

/// <summary>The OpenTelemetry SDK's standard environment variable names, also used as keys in the OpenTelemetry config section.</summary>
public static class OpenTelemetryEnvironmentVariables
{
    public const string ServiceName = "OTEL_SERVICE_NAME";
    public const string ExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string ExporterOtlpProtocol = "OTEL_EXPORTER_OTLP_PROTOCOL";
    public const string ResourceAttributes = "OTEL_RESOURCE_ATTRIBUTES";
}
