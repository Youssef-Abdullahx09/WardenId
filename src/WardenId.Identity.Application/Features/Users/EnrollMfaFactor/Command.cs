using FluentResults;
using MediatR;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.EnrollMfaFactor;

public sealed record Command(
    Guid TenantId,
    Guid UserId,
    MfaFactorType FactorType,
    string CredentialData) : IRequest<Result<MfaFactorEnrolled>>;
