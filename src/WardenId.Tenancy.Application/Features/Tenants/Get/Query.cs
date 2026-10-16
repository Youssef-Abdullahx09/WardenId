using FluentResults;
using MediatR;

namespace WardenId.Tenancy.Application.Features.Tenants.Get;

public sealed record Query(Guid TenantId) : IRequest<Result<TenantDetail>>;
