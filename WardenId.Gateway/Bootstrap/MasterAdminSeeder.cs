using MediatR;
using WardenId.Audit.Application;
using WardenId.Audit.Domain;
using WardenId.Gateway.Authorization;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Features.Users.Register;
using WardenId.Identity.Domain;
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

        // Checked explicitly, same pattern as MasterTenantSeeder, so a genuine seed failure
        // (e.g. the configured password failing policy/breach checks) is never confused with
        // this ordinary "already ran before" case — Register's own uniqueness error and a
        // policy rejection both come back as an equally generic Result.Fail otherwise.
        var emailResult = Email.Create(options.AdminEmail);
        if (emailResult.IsSuccess)
        {
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            if (await userRepository.ExistsByEmailAsync(tenantId, emailResult.Value, CancellationToken.None))
            {
                logger.LogInformation(
                    "Master admin user '{Email}' already exists — skipping seed.", options.AdminEmail);
                await AllowOnAdminApiAsync(app, userRepository, tenantId, emailResult.Value, logger);
                return;
            }
        }

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
                // The existence check above already accounted for the "ran before" case, so
                // anything reaching here is a real, actionable failure (e.g. the configured
                // password rejected by policy/breach checks) — it needs to be loud, not buried
                // at Info alongside routine no-ops.
                logger.LogWarning(
                    "Master admin user seed for '{Email}' failed: {Errors}",
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

        if (emailResult.IsSuccess)
        {
            await AllowOnAdminApiAsync(
                app, scope.ServiceProvider.GetRequiredService<IUserRepository>(), tenantId, emailResult.Value, logger);
        }
    }

    /// <summary>
    /// The seeded master admin is a platform admin on the admin API (until roles exist, B2), whether
    /// this run created the user or found it from an earlier one.
    /// </summary>
    private static async Task AllowOnAdminApiAsync(
        WebApplication app, IUserRepository users, Guid tenantId, Email email, ILogger logger)
    {
        var user = await users.GetByEmailAsync(tenantId, email, CancellationToken.None);
        if (user.IsFailed) return;

        app.Services.GetRequiredService<PlatformAdmins>().Add(tenantId, user.Value.Id);
        logger.LogInformation(
            "Master admin user '{Email}' ({UserId}) may use the admin API.", email.Value, user.Value.Id);
    }
}

internal sealed class MasterTenantAdminOptions
{
    public string? Slug { get; init; }
    public string? AdminEmail { get; init; }
    public string? AdminUsername { get; init; }
    public string? AdminPassword { get; init; }
}
