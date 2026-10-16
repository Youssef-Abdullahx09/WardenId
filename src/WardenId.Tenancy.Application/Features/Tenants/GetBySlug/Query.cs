using FluentResults;
using MediatR;
using WardenId.Tenancy.Application.Features.Tenants.Get;

namespace WardenId.Tenancy.Application.Features.Tenants.GetBySlug;

public sealed record Query(string Slug) : IRequest<Result<TenantDetail>>;
