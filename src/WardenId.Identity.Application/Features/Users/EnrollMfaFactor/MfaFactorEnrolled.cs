namespace WardenId.Identity.Application.Features.Users.EnrollMfaFactor;

public sealed record MfaFactorEnrolled(Guid FactorId, IReadOnlyCollection<string> RawRecoveryCodes);
