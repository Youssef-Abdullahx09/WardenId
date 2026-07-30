using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Get;

public sealed record Query(Guid TenantId, Guid UserId) : IRequest<Result<UserDetail>>;
