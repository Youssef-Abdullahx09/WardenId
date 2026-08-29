using FluentResults;
using MediatR;

namespace WardenId.Tenancy.Application.Features.SigningKeys.GetActiveForSigning;

public sealed record Query(Guid TenantId) : IRequest<Result<ActiveSigningKeyDto>>;
