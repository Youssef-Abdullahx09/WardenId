using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.VerifyEmail;

public sealed record Command(Guid TenantId, Guid UserId, string RawToken) : IRequest<Result>;
