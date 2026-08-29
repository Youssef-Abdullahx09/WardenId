using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Login;

public sealed record Command(Guid TenantId, string EmailOrUsername, string Password) : IRequest<Result<LoginResult>>;
