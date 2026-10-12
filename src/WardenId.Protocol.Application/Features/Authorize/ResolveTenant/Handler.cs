using FluentResults;
using MediatR;
using WardenId.Protocol.Domain;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;
using ResolveForIssuanceQuery = WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance.Query;

namespace WardenId.Protocol.Application.Features.Authorize.ResolveTenant;

public sealed class Handler(ISender sender) : IRequestHandler<Query, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Route constraints are case-insensitive; issuers are not.
        if (!TenantSlugFormat.IsValid(request.Slug)) return Result.Fail(ProtocolErrors.TenantNotFound());

        var result = await sender.Send(new ResolveForIssuanceQuery(request.Slug), cancellationToken);
        if (result.IsFailed || result.Value.Status != TenantIssuanceStatus.Active)
            return Result.Fail(ProtocolErrors.TenantNotFound());

        return Result.Ok(result.Value.TenantId);
    }
}
