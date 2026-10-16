using FluentResults;
using MediatR;
using WardenId.Tenancy.Application.Abstractions;

namespace WardenId.Tenancy.Application.Features.Tenants.Get;

public sealed class Handler(ITenantRepository repository) : IRequestHandler<Query, Result<TenantDetail>>
{
    public async Task<Result<TenantDetail>> Handle(Query request, CancellationToken cancellationToken)
    {
        var tenant = await repository.GetByIdAsync(request.TenantId, cancellationToken);
        return tenant.IsFailed ? Result.Fail(tenant.Errors) : Result.Ok(TenantDetail.From(tenant.Value));
    }
}
