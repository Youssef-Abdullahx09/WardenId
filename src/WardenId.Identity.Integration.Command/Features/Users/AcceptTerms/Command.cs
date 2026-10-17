using FluentResults;
using MediatR;

namespace WardenId.Identity.Integration.Command.Features.Users.AcceptTerms;

/// <summary>Records that the user accepted the tenant's terms now and clears the AcceptTos required action.</summary>
public sealed record Command(Guid TenantId, Guid UserId) : IRequest<Result>;
