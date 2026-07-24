using FluentResults;
using MediatR;

namespace WardenId.Tenancy.Application.Features.Tenants.Create;

public sealed record Command(
    string Slug,
    string DisplayName,
    Guid UserId) : IRequest<Result<Guid>>;