using FluentResults;
using MediatR;

namespace WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys;

public sealed record Query(string Slug) : IRequest<Result<TenantPublicKeys>>;
