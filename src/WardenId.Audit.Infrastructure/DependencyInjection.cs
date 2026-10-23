using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WardenId.Audit.Application;
using WardenId.Audit.Application.Abstractions;
using WardenId.Audit.Infrastructure.Exporting;
using WardenId.Audit.Infrastructure.Persistence;
using WardenId.Audit.Infrastructure.Retention;
using WardenId.Audit.Infrastructure.Sealing;
using WardenId.Audit.Infrastructure.Witnessing;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.Persistence;

namespace WardenId.Audit.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Uses a pooled DbContext factory rather than plain AddDbContext (Tenancy's pattern) —
    /// Audit's consumers (this module's own AuditRecorder, and the phase-3 sealer background
    /// service) are not request-scoped, so nothing here can assume a DI scope exists.
    /// </summary>
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var defaultConnectionString = configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey)
            ?? throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage);

        services.AddPooledDbContextFactory<AuditDbContext>(options =>
            options.UseNpgsql(
                defaultConnectionString,
                postgresSql => postgresSql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, AuditDbContext.Schema)));

        services.AddSingleton<IAuditContextAccessor, AsyncLocalAuditContextAccessor>();
        services.AddSingleton<IAuditActorScope, AuditActorScope>();
        services.AddSingleton<IAuditRecorder, AuditRecorder>();
        services.AddSingleton<IAuditProofRepository, AuditProofRepository>();
        services.AddSingleton<IAuditEventSearch, AuditEventSearch>();

        // Stateless — safe as a singleton. Registered as ISaveChangesInterceptor so every module's
        // DbContextOptionsBuilder.AddSharedInterceptors(...) (WardenId.SharedKernel.Persistence)
        // picks it up automatically — no per-module wiring needed (see docs/audit/PLAN.md §5.2).
        services.AddSingleton<ISaveChangesInterceptor, AuditCaptureInterceptor>();

        services.Configure<AuditSealerOptions>(configuration.GetSection(ConfigurationKeys.Sealer.Section));
        services.AddSingleton<ISealableEventReader, PgSealableEventReader>();
        services.AddSingleton<IChannelAdvisoryLock, PgChannelAdvisoryLock>();
        services.AddSingleton<IAuditCheckpointSigner, Es256CheckpointSigner>();
        services.AddSingleton<IAuditCheckpointKeyRotator, Es256CheckpointKeyRotator>();

        var noWitnessConfigured = !AddCheckpointWitnesses(services, configuration);
        services.AddHostedService<AuditSealerService>();

        services.Configure<AuditExporterOptions>(configuration.GetSection(ConfigurationKeys.Exporter.Section));
        services.AddSingleton<IAuditSegmentPublisher, CapAuditSegmentPublisher>();
        services.AddHostedService<AuditExporterService>();

        services.Configure<AuditRetentionOptions>(configuration.GetSection(ConfigurationKeys.Retention.Section));
        var retentionConnectionString = configuration.GetConnectionString(ConfigurationKeys.Retention.RetentionConnectionStringKey);
        var noSeparateRetentionRole = string.IsNullOrWhiteSpace(retentionConnectionString);
        services.AddSingleton<IAuditRetentionDbContextFactory>(
            new AuditRetentionDbContextFactory(noSeparateRetentionRole ? defaultConnectionString : retentionConnectionString!));
        services.AddHostedService<AuditRetentionService>();

        InitializeAsync(services, noWitnessConfigured, noSeparateRetentionRole).GetAwaiter().GetResult();

        return services;
    }

    private static async Task InitializeAsync(IServiceCollection services, bool noWitnessConfigured, bool noSeparateRetentionRole)
    {
        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDbContextFactory<AuditDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();

        LogStartupWarnings(provider.GetRequiredService<ILogger<AuditDbContext>>(), noWitnessConfigured, noSeparateRetentionRole);
    }

    /// <summary>Returns true iff at least one witness was registered.</summary>
    private static bool AddCheckpointWitnesses(IServiceCollection services, IConfiguration configuration)
    {
        var witnessOptions = configuration.GetSection(ConfigurationKeys.Witness.Section).Get<AuditWitnessOptions>() ?? new AuditWitnessOptions();
        services.Configure<AuditWitnessOptions>(configuration.GetSection(ConfigurationKeys.Witness.Section));

        var anyConfigured = false;

        if (!string.IsNullOrWhiteSpace(witnessOptions.AppendOnlyFilePath))
        {
            services.AddSingleton<IAuditCheckpointWitness, FileAppendCheckpointWitness>();
            anyConfigured = true;
        }

        if (!string.IsNullOrWhiteSpace(witnessOptions.WebhookUrl))
        {
            services.AddHttpClient<WebhookCheckpointWitness>();
            services.AddSingleton<IAuditCheckpointWitness>(sp => sp.GetRequiredService<WebhookCheckpointWitness>());
            anyConfigured = true;
        }

        return anyConfigured;
    }

    // Spec §8's "when neither configured, log a startup warning stating the audit log is only
    // as durable as the database [or app role separation]" — applied to both the external
    // checkpoint witnesses and the retention role-separation gap (docs/audit/OPEN-DECISIONS.md #8).
    private static void LogStartupWarnings(ILogger<AuditDbContext> logger, bool noWitnessConfigured, bool noSeparateRetentionRole)
    {
        if (noWitnessConfigured)
        {
            logger.LogWarning(
                "No audit checkpoint witness (Audit:Witness:AppendOnlyFilePath / WebhookUrl) is configured — " +
                "the signed checkpoint chain cannot detect tail truncation or a dropped audit schema without an " +
                "external witness; the audit log's durability depends entirely on this database.");
        }

        if (noSeparateRetentionRole)
        {
            logger.LogWarning(
                "No dedicated retention connection string (ConnectionStrings:{0}) is configured — the retention " +
                "purge path is running under the same DB role as the rest of the app rather than a role scoped to " +
                "only UPDATE(Payload) on audit.events; see docs/audit/db-roles.sql.",
                ConfigurationKeys.Retention.RetentionConnectionStringKey);
        }
    }
}
