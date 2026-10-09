using FluentResults;
using MediatR;

namespace WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;

public sealed record Query(string Slug) : IRequest<Result<TenantForIssuance>>;
