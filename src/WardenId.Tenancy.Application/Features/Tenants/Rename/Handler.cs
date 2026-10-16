using FluentResults;
using MediatR;
using WardenId.Tenancy.Application.Abstractions;

namespace WardenId.Tenancy.Application.Features.Tenants.Rename;

public sealed class Handler(ITenantRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var tenantResult = await repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenantResult.IsFailed) return Result.Fail(tenantResult.Errors);

        var tenant = tenantResult.Value;
        var result = tenant.Rename(request.DisplayName);
        if (result.IsFailed) return result;

        await repository.UpdateAsync(tenant, request.UserId, cancellationToken);
        return Result.Ok();
    }
}
