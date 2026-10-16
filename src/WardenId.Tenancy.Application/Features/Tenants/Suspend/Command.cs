using FluentResults;
using MediatR;

namespace WardenId.Tenancy.Application.Features.Tenants.Suspend;

public sealed record Command(Guid TenantId, string Reason, Guid UserId) : IRequest<Result>;
