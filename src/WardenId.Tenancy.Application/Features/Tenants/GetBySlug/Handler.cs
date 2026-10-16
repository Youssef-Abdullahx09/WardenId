using FluentResults;
using MediatR;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Application.Features.Tenants.Get;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Features.Tenants.GetBySlug;

public sealed class Handler(ITenantRepository repository) : IRequestHandler<Query, Result<TenantDetail>>
{
    public async Task<Result<TenantDetail>> Handle(Query request, CancellationToken cancellationToken)
    {
        // A malformed slug can't name a tenant: answer "not found", not a validation error.
        var slug = Slug.Create(request.Slug);
        if (slug.IsFailed) return Result.Fail(TenantErrors.NotFound);

        var tenant = await repository.GetBySlugAsync(slug.Value, cancellationToken);
        return tenant.IsFailed ? Result.Fail(tenant.Errors) : Result.Ok(TenantDetail.From(tenant.Value));
    }
}
