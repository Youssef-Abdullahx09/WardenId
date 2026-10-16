using FluentResults;
using MediatR;

namespace WardenId.Tenancy.Application.Features.SigningKeys.Rotate;

/// <summary>Returns the new signing key's id (its JWK "kid").</summary>
public sealed record Command(Guid TenantId, Guid UserId) : IRequest<Result<Guid>>;
