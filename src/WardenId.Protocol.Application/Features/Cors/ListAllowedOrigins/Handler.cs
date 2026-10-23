using MediatR;
using WardenId.SharedKernel.Domain;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;
using ListCorsOriginsQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.ListCorsOrigins.Query;
using ResolveForIssuanceQuery = WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance.Query;

namespace WardenId.Protocol.Application.Features.Cors.ListAllowedOrigins;

public sealed class Handler(ISender sender) : IRequestHandler<Query, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Route constraints are case-insensitive; issuers are not.
        if (!TenantSlugFormat.IsValid(request.Slug)) return [];

        var tenant = await sender.Send(new ResolveForIssuanceQuery(request.Slug), cancellationToken);
        if (tenant.IsFailed || tenant.Value.Status != TenantIssuanceStatus.Active) return [];

        var origins = await sender.Send(new ListCorsOriginsQuery(tenant.Value.TenantId), cancellationToken);
        return origins.IsSuccess ? origins.Value : [];
    }
}
