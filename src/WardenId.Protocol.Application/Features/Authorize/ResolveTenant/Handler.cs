using FluentResults;
using MediatR;
using WardenId.Protocol.Domain;
using WardenId.SharedKernel.Domain.Utilities;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;
using ResolveForIssuanceQuery = WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance.Query;

namespace WardenId.Protocol.Application.Features.Authorize.ResolveTenant;

public sealed class Handler(ISender sender) : IRequestHandler<Query, Result<ResolvedTenant>>
{
    public async Task<Result<ResolvedTenant>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Route constraints are case-insensitive; issuers are not.
        if (!TenantSlugFormat.IsValid(request.Slug)) return Result.Fail(ProtocolErrors.TenantNotFound());

        var result = await sender.Send(new ResolveForIssuanceQuery(request.Slug), cancellationToken);
        if (result.IsFailed || result.Value.Status != TenantIssuanceStatus.Active)
            return Result.Fail(ProtocolErrors.TenantNotFound());

        var tenant = result.Value;
        var displayName = string.IsNullOrWhiteSpace(tenant.DisplayName) ? tenant.Slug : tenant.DisplayName.Trim();
        return Result.Ok(new ResolvedTenant(tenant.TenantId, displayName));
    }
}
