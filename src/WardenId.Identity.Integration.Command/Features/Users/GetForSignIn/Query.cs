using FluentResults;
using MediatR;

namespace WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;

/// <summary>Re-reads a user at token issuance (e.g. code redemption) — no credentials involved.</summary>
public sealed record Query(Guid TenantId, Guid UserId) : IRequest<Result<SignInUser>>;
