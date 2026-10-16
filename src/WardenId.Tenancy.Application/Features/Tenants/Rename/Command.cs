using FluentResults;
using MediatR;

namespace WardenId.Tenancy.Application.Features.Tenants.Rename;

public sealed record Command(Guid TenantId, string DisplayName, Guid UserId) : IRequest<Result>;
