using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Authorize.ResolveTenant;

/// <summary>Slug → tenant id for interactive pages outside the OpenIddict pipeline. Suspended counts as not found.</summary>
public sealed record Query(string Slug) : IRequest<Result<Guid>>;
