using MediatR;
using WardenId.Audit.Application;
using WardenId.Audit.Domain;
using WardenId.Identity.Application.Features.Users.Register;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Infrastructure;

namespace WardenId.Gateway.Bootstrap;

/// <summary>
/// Seeds the platform's first admin user into whatever master tenant WardenId.Tenancy already
/// seeded (looked up by slug — Tenant.Id is generated internally and can't be pinned ahead of
/// time). Runs after the app is fully built so it can reuse the real Users/Register command
/// through ISender, getting password policy, breach checking, and verification-token issuance
/// for free — no new user-creation code path.
/// </summary>
public static class MasterAdminSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        var section = app.Configuration.GetSection(ConfigurationKeys.MasterTenant.Section);
        if (!section.Exists()) return;

        var options = section.Get<MasterTenantAdminOptions>() ?? new MasterTenantAdminOptions();
        if (string.IsNullOrWhiteSpace(options.Slug) ||
            string.IsNullOrWhiteSpace(options.AdminEmail) ||
            string.IsNullOrWhiteSpace(options.AdminUsername) ||
            string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            throw new InvalidOperationException(
                $"Configuration section '{ConfigurationKeys.MasterTenant.Section}' is present but " +
                $"'{nameof(MasterTenantAdminOptions.Slug)}', '{nameof(MasterTenantAdminOptions.AdminEmail)}', " +
                $"'{nameof(MasterTenantAdminOptions.AdminUsername)}' and '{nameof(MasterTenantAdminOptions.AdminPassword)}' are required.");
        }

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("WardenId.Gateway.MasterAdminSeed");

        var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var slug = Slug.FromTrustedSource(options.Slug);
        var tenantResult = await tenantRepository.GetBySlugAsync(slug, CancellationToken.None);
        if (tenantResult.IsFailed)
        {
            logger.LogError(
                "Master tenant '{Slug}' not found — skipping admin user seed. {Errors}",
                slug.Value, string.Join("; ", tenantResult.Errors.Select(e => e.Message)));
            return;
        }

        var tenantId = tenantResult.Value.Id;
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Runs before the request pipeline exists, so AuditContextMiddleware never sets ambient
        // audit context here — AuditCaptureInterceptor fails closed without it (I10). Bootstrap
        // seeding is the one legitimate non-HTTP writer of a User today; a System actor records
        // that plainly rather than faking an end-user identity.
        var auditContextAccessor = scope.ServiceProvider.GetRequiredService<IAuditContextAccessor>();
        var systemActor = Actor.Create(ActorType.System, "master-admin-seeder", ActorScope.Platform()).Value;
        using var auditScope = auditContextAccessor.BeginScope(
            new AuditAmbientContext(systemActor, tenantId, Correlation.None(), ClientContext.Unknown()));

        try
        {
            var registerResult = await sender.Send(
                new Command(tenantId, options.AdminEmail, options.AdminUsername, options.AdminPassword),
                CancellationToken.None);

            if (registerResult.IsSuccess)
            {
                logger.LogInformation(
                    "Master admin user '{Email}' seeded for tenant '{Slug}'.", options.AdminEmail, slug.Value);
            }
            else
            {
                // Most commonly hit on every restart after the first — the admin user already
                // exists (Register enforces uniqueness itself). Treated as a no-op, not an error.
                logger.LogInformation(
                    "Master admin user seed for '{Email}' was a no-op: {Errors}",
                    options.AdminEmail, string.Join("; ", registerResult.Errors.Select(e => e.Message)));
            }
        }
        catch (Exception ex)
        {
            // The user row is persisted before Register's follow-up email send — a broken SMTP
            // config shouldn't take the whole app down over an already-usable bootstrap account.
            logger.LogWarning(ex,
                "Master admin user '{Email}' may have been created, but a follow-up step " +
                "(e.g. sending the verification email) failed.",
                options.AdminEmail);
        }
    }
}

internal sealed class MasterTenantAdminOptions
{
    public string? Slug { get; init; }
    public string? AdminEmail { get; init; }
    public string? AdminUsername { get; init; }
    public string? AdminPassword { get; init; }
}
