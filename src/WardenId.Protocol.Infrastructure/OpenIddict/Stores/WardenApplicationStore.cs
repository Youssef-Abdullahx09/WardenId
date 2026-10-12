using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.SharedKernel.Application.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using GetForAuthenticationQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication.Query;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Stores;

/// <summary>
/// Read-only application store answered by ClientManagement (the only writer of clients).
/// client_id is unique per tenant only, so lookups use the tenant resolved for this request.
/// Entities are transient and never persisted; Applications table stays empty.
/// </summary>
public sealed class WardenApplicationStore(
    IMemoryCache cache,
    IOpenIddictEntityFrameworkCoreContext context,
    IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions> options,
    ISender sender,
    ITenantContext tenantContext)
    : OpenIddictEntityFrameworkCoreApplicationStore(cache, context, options)
{
    // Per request (scoped store): lets FindByIdAsync answer for an application found by client id.
    private readonly Dictionary<string, OpenIddictEntityFrameworkCoreApplication> _resolved = new(StringComparer.Ordinal);

    public override async ValueTask<OpenIddictEntityFrameworkCoreApplication?> FindByClientIdAsync(
        string identifier, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId) return null;

        var result = await sender.Send(new GetForAuthenticationQuery(tenantId, identifier), cancellationToken);
        if (result.IsFailed || !result.Value.Enabled) return null;

        var application = ClientApplicationMapper.Map(result.Value);
        _resolved[application.Id!] = application;
        return application;
    }

    public override ValueTask<OpenIddictEntityFrameworkCoreApplication?> FindByIdAsync(
        string identifier, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_resolved.GetValueOrDefault(identifier));

    public override ValueTask CreateAsync(OpenIddictEntityFrameworkCoreApplication application, CancellationToken cancellationToken) =>
        throw ReadOnly();

    public override ValueTask UpdateAsync(OpenIddictEntityFrameworkCoreApplication application, CancellationToken cancellationToken) =>
        throw ReadOnly();

    public override ValueTask DeleteAsync(OpenIddictEntityFrameworkCoreApplication application, CancellationToken cancellationToken) =>
        throw ReadOnly();

    private static NotSupportedException ReadOnly() => new("Clients are owned by the ClientManagement module.");
}
