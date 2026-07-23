using System.Text.Json.Serialization;
using DotNetCore.CAP;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.SharedKernel.Infrastructure;

public static class DependencyInjection
{
    // appsettings holds the OpenTelemetry SDK's own standard env var names (OTEL_SERVICE_NAME,
    // OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_RESOURCE_ATTRIBUTES) as fallback values. At startup they
    // are applied to the process environment only where not already set, so a real container/CI
    // env var always wins; once set, the OTel SDK's own env-var support (AddEnvironmentVariableDetector,
    // OtlpExporterOptions' built-in binding) reads them with no further code needed here.
    private static readonly string[] OpenTelemetryEnvironmentVariableNames =
    [
        "OTEL_SERVICE_NAME",
        "OTEL_EXPORTER_OTLP_ENDPOINT",
        "OTEL_RESOURCE_ATTRIBUTES"
    ];

    public static void AddSharedKernelInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCapService(configuration);
        services.AddEmailService(configuration);
        services.AddOpenTelemetryObservability(configuration);
    }

    private static bool ReadOpenTelemetryEnabled(IConfiguration configuration) =>
        configuration.GetSection(OpenTelemetryOptions.SectionName)
            .GetValue<bool?>(nameof(OpenTelemetryOptions.Enabled)) ?? true;

    private static void ApplyOpenTelemetryEnvironmentVariables(IConfiguration configuration)
    {
        var section = configuration.GetSection(OpenTelemetryOptions.SectionName);

        foreach (var name in OpenTelemetryEnvironmentVariableNames)
        {
            var value = section[name];
            if (!string.IsNullOrEmpty(value) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    private static void AddCapService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCap(options =>
        {
            var capSection = configuration.GetSection(ConfigurationKeys.CapPublisher.Section);
            var connectionString = configuration.GetConnectionString(ConfigurationKeys.DbConnectionStringKey);
            
            options.UsePostgreSql(string.IsNullOrWhiteSpace(connectionString)
                ? throw new InvalidOperationException(
                    $"Connection string '{ConfigurationKeys.DbConnectionStringKey}' is missing or empty.")
                : connectionString);
            
            options.FallbackWindowLookbackSeconds = 
                capSection.GetValue<int?>(ConfigurationKeys.CapPublisher.FallbackWindowLookBackSeconds) ?? 3; 
            options.FailedRetryCount =
                capSection.GetValue<int?>(ConfigurationKeys.CapPublisher.RetryCount) ?? 3;
            options.FailedRetryInterval =
                capSection.GetValue<int?>(ConfigurationKeys.CapPublisher.RetryInterval) ?? 1000; 
            options.SucceedMessageExpiredAfter =
                capSection.GetValue<int?>(ConfigurationKeys.CapPublisher.SucceedMessageExpiredAfter) ??
                3600; 
            options.FailedMessageExpiredAfter =
                capSection.GetValue<int?>(ConfigurationKeys.CapPublisher.FailedMessageExpiredAfter) ??
                3600; 
            
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

            var rabbitMqSection = configuration.GetSection(ConfigurationKeys.RabbitMq.Section);
            options.UseRabbitMQ(configure =>
            {
                configure.HostName = rabbitMqSection[ConfigurationKeys.RabbitMq.Host]
                                     ?? throw new InvalidOperationException(ConfigurationKeys.RabbitMq.Host);
                configure.Port = rabbitMqSection.GetValue<int?>(ConfigurationKeys.RabbitMq.Port) ?? 5672;
                configure.UserName = rabbitMqSection[ConfigurationKeys.RabbitMq.Username]
                                     ?? throw new InvalidOperationException(ConfigurationKeys.RabbitMq.Username);
                configure.Password = rabbitMqSection[ConfigurationKeys.RabbitMq.Password]
                                     ?? throw new InvalidOperationException(ConfigurationKeys.RabbitMq.Password);
                configure.BasicQosOptions = new RabbitMQOptions.BasicQos(
                    ushort.Parse(rabbitMqSection[ConfigurationKeys.RabbitMq.PrefetchCount] ?? "10"));
            });
        });
    }

    private static void AddEmailService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var smtpSection = configuration.GetSection(ConfigurationKeys.Smtp.Section);

        var smtpSettings = new SmtpSettings
        {
            Host = smtpSection[ConfigurationKeys.Smtp.Host]
                   ?? throw new InvalidOperationException(ConfigurationKeys.Smtp.Host),
            Port = smtpSection.GetValue<int?>(ConfigurationKeys.Smtp.Port) ?? 587,
            Username = smtpSection[ConfigurationKeys.Smtp.Username],
            Password = smtpSection[ConfigurationKeys.Smtp.Password],
            EnableSsl = smtpSection.GetValue<bool?>(ConfigurationKeys.Smtp.EnableSsl) ?? true,
            FromAddress = smtpSection[ConfigurationKeys.Smtp.FromAddress]
                          ?? throw new InvalidOperationException(ConfigurationKeys.Smtp.FromAddress),
            FromDisplayName = smtpSection[ConfigurationKeys.Smtp.FromDisplayName],
        };

        services.AddSingleton(smtpSettings);
        services.AddScoped<IEmailService, SmtpEmailService>();
    }

    public static void AddOpenTelemetryLogging(
        this ILoggingBuilder logging,
        IConfiguration configuration)
    {
        if (!ReadOpenTelemetryEnabled(configuration)) return;

        ApplyOpenTelemetryEnvironmentVariables(configuration);

        logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddEnvironmentVariableDetector());
            options.AddOtlpExporter();
        });
    }

    private static void AddOpenTelemetryObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OpenTelemetryOptions>()
            .Bind(configuration.GetSection(OpenTelemetryOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (!ReadOpenTelemetryEnabled(configuration)) return;

        ApplyOpenTelemetryEnvironmentVariables(configuration);

        var resourceBuilder = ResourceBuilder.CreateDefault().AddEnvironmentVariableDetector();

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSqlClientInstrumentation()
                .AddCapInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());
    }
}