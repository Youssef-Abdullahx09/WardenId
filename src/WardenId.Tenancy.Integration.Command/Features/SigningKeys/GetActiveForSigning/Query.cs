using FluentResults;
using MediatR;

namespace WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning;

public sealed record Query(Guid TenantId) : IRequest<Result<ActiveSigningKey>>;
