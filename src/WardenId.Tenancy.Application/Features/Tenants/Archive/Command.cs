using FluentResults;
using MediatR;

namespace WardenId.Tenancy.Application.Features.Tenants.Archive;

public sealed record Command(Guid TenantId, Guid UserId) : IRequest<Result>;
