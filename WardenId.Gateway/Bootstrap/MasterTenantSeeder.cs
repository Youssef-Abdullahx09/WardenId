using WardenId.Audit.Application;
using WardenId.Audit.Domain;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Infrastructure;

namespace WardenId.Gateway.Bootstrap;

/// <summary>
/// Idempotently seeds the platform's master tenant if the "MasterTenant" config section is
/// present. Runs after the app is fully built (and its host started) so CAP's own storage —
/// e.g. the "cap.published" outbox table — has been created by CAP's hosted bootstrapper before
/// this hits SaveChangesAsync; running this during DI registration (the previous approach) raced
/// that table into existence and failed with "relation cap.published does not exist". Never
/// touches an already-existing tenant — config changes have no effect once the tenant has been
/// created.
/// </summary>
public static class MasterTenantSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        var section = app.Configuration.GetSection(ConfigurationKeys.MasterTenant.Section);
        if (!section.Exists()) return;

        var options = section.Get<MasterTenantSeedOptions>() ?? new MasterTenantSeedOptions();
        if (string.IsNullOrWhiteSpace(options.Slug) || string.IsNullOrWhiteSpace(options.DisplayName))
        {
            throw new InvalidOperationException(
                $"Configuration section '{ConfigurationKeys.MasterTenant.Section}' is present but " +
                $"'{nameof(MasterTenantSeedOptions.Slug)}' and '{nameof(MasterTenantSeedOptions.DisplayName)}' are required.");
        }

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("WardenId.Gateway.MasterTenantSeed");

        var slugResult = Slug.Create(options.Slug);
        if (slugResult.IsFailed)
        {
            throw new InvalidOperationException(
                $"Configured master tenant slug '{options.Slug}' is invalid: " +
                string.Join("; ", slugResult.Errors.Select(e => e.Message)));
        }

        var slug = slugResult.Value;
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        if (await repository.ExistsBySlugAsync(slug, CancellationToken.None))
        {
            logger.LogInformation("Master tenant '{Slug}' already exists — skipping seed.", slug.Value);
            return;
        }

        var signingKeyGenerator = scope.ServiceProvider.GetRequiredService<ISigningKeyGenerator>();
        var tenantResult = TenantProvisioning.CreateActivated(slug, options.DisplayName, signingKeyGenerator);
        if (tenantResult.IsFailed)
        {
            throw new InvalidOperationException(
                "Failed to seed master tenant: " + string.Join("; ", tenantResult.Errors.Select(e => e.Message)));
        }

        var tenant = tenantResult.Value;

        // No request, so no AuditContextMiddleware: AuditCaptureInterceptor fails closed without an
        // ambient audit context, and a Tenant is audited. Same System actor pattern as MasterAdminSeeder.
        var auditContextAccessor = scope.ServiceProvider.GetRequiredService<IAuditContextAccessor>();
        var systemActor = Actor.Create(ActorType.System, "master-tenant-seeder", ActorScope.Platform()).Value;
        using (auditContextAccessor.BeginScope(
                   new AuditAmbientContext(systemActor, tenant.Id, Correlation.None(), ClientContext.Unknown())))
        {
            await repository.CreateAsync(tenant, Guid.Empty, CancellationToken.None);
        }

        logger.LogInformation("Master tenant '{Slug}' seeded with id {TenantId}.", slug.Value, tenant.Id);
    }
}

internal sealed class MasterTenantSeedOptions
{
    public string? Slug { get; init; }
    public string? DisplayName { get; init; }
}
