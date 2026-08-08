using WardenId.Identity.Application.Features.Users.EnrollMfaFactor;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Presentation.Features.Users.EnrollMfaFactor;

public sealed record Request(
    MfaFactorType FactorType,
    string CredentialData)
{
    public Command ToCommand(Guid tenantId, Guid userId) => new(tenantId, userId, FactorType, CredentialData);
}
