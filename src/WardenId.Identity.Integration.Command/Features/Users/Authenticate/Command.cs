using FluentResults;
using MediatR;

namespace WardenId.Identity.Integration.Command.Features.Users.Authenticate;

public sealed record Command(Guid TenantId, string EmailOrUsername, string Password) : IRequest<Result<AuthenticatedUser>>;
