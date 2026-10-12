using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Authorize.SignIn;

public sealed record Command(Guid TenantId, string EmailOrUsername, string Password) : IRequest<Result<SignInOutcome>>;
