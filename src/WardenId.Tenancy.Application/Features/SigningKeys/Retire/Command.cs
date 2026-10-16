using FluentResults;
using MediatR;

namespace WardenId.Tenancy.Application.Features.SigningKeys.Retire;

public sealed record Command(Guid TenantId, Guid SigningKeyId, Guid UserId) : IRequest<Result>;
