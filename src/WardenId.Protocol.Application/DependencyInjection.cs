using System.Reflection;
using DotNetCore.CAP;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Application.Features.Policies;
using WardenId.Protocol.Application.Features.Token;
using WardenId.Protocol.Application.Utilities;

namespace WardenId.Protocol.Application;

public static class DependencyInjection
{
    public static void AddProtocolApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly());
            // Needs IAuditActorScope, which the Audit module registers.
            cfg.AddOpenBehavior(typeof(ActAsUserBehavior<,>));
        });
        services.Configure<ProtocolTokenOptions>(configuration.GetSection(ConfigurationKeys.ProtocolToken.Section));
        services.Configure<ProtocolSessionOptions>(configuration.GetSection(ConfigurationKeys.ProtocolSession.Section));
        // TryAdd: tests register a FakeTimeProvider first; OpenIddict resolves the same TimeProvider.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<UserTokenGrantFactory>();
        // Per-tenant lifetimes on the issuance path, cached briefly per tenant (TenantLifetimes.CacheDuration).
        services.AddMemoryCache();
        services.AddScoped<ITenantLifetimes, TenantLifetimes>();
        services.AddScoped<IAuthAuditTrail, SenderAuthAuditTrail>();
        services.AddSubscribers();
    }

    // CAP discovers subscribers from the service collection: an unregistered ICapSubscribe class never runs.
    private static void AddSubscribers(
        this IServiceCollection services)
    {
        services.AddScoped<ICapSubscribe, Subscribers.SessionEndedDomainEvent.Subscriber>();
        services.AddScoped<ICapSubscribe, Subscribers.BackchannelLogoutRequested.Subscriber>();
        services.AddScoped<ICapSubscribe, Subscribers.PasswordChangedDomainEvent.Subscriber>();
        services.AddScoped<ICapSubscribe, Subscribers.PasswordResetDomainEvent.Subscriber>();
        services.AddScoped<ICapSubscribe, Subscribers.UserDisabledDomainEvent.Subscriber>();
        services.AddScoped<ICapSubscribe, Subscribers.UserSoftDeletedDomainEvent.Subscriber>();
        services.AddScoped<ICapSubscribe, Subscribers.AccountLockedOutDomainEvent.Subscriber>();
    }
}
