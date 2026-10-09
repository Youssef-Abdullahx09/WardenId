using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using OpenIddict.EntityFrameworkCore.Models;
using WardenId.SharedKernel.Application.Abstractions;
using VerifySecretQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.VerifySecret.Query;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Stores;

/// <summary>Delegates client secret verification to ClientManagement (hashing + rotation live there).</summary>
public sealed class WardenApplicationManager(
    IOpenIddictApplicationCache<OpenIddictEntityFrameworkCoreApplication> cache,
    ILogger<OpenIddictApplicationManager<OpenIddictEntityFrameworkCoreApplication>> logger,
    IOptionsMonitor<OpenIddictCoreOptions> options,
    IOpenIddictApplicationStore<OpenIddictEntityFrameworkCoreApplication> store,
    ISender sender,
    ITenantContext tenantContext)
    : OpenIddictApplicationManager<OpenIddictEntityFrameworkCoreApplication>(cache, logger, options, store)
{
    public override async ValueTask<bool> ValidateClientSecretAsync(
        OpenIddictEntityFrameworkCoreApplication application, string secret, CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is not { } tenantId
            || application.ClientId is null)
        {
            return false;
        }

        var result = await sender.Send(new VerifySecretQuery(tenantId, application.ClientId, secret), cancellationToken);
        return result.IsSuccess;
    }
}
