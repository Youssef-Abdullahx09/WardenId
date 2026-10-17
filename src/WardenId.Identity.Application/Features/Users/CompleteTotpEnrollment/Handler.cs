using System.Security.Cryptography;
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Mfa;
using WardenId.Identity.Domain;
using Command = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.Command;
using CompletedTotpEnrollment = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.CompletedTotpEnrollment;

namespace WardenId.Identity.Application.Features.Users.CompleteTotpEnrollment;

/// <summary>
/// A wrong first code doesn't count towards the lockout: it only fails to enable a factor the user is setting up,
/// and the password step before it already counted. The user can try again with the same factor.
/// </summary>
public sealed class Handler(
    IUserRepository repository,
    ITotpSecretProtector secretProtector,
    TimeProvider timeProvider) : IRequestHandler<Command, Result<CompletedTotpEnrollment>>
{
    public async Task<Result<CompletedTotpEnrollment>> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);
        var user = userResult.Value;

        var factor = user.MfaFactors.FirstOrDefault(f =>
            f.Id == request.FactorId && f.FactorType == MfaFactorType.Totp && f.IsPendingVerification);
        if (factor is null) return Result.Fail(UserErrors.MfaFactorNotFound());

        var now = timeProvider.GetUtcNow();
        var secret = secretProtector.TryUnprotect(factor.CredentialData);
        if (secret is null) return Result.Fail(UserErrors.MfaFactorNotFound());

        IReadOnlyList<long> steps;
        try
        {
            steps = Totp.MatchingTimeSteps(secret, request.Code, now);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        if (steps.Count == 0) return Result.Fail(UserErrors.MfaCodeInvalid());

        var (rawCodes, codes) = RecoveryCodes.Generate();
        var completeResult = user.CompleteMfaEnrollment(factor.Id, steps[^1], codes, now);
        if (completeResult.IsFailed) return Result.Fail(completeResult.Errors);

        // Lost a race (say, the same first code sent twice): nothing was saved, and this request is answered like a
        // wrong code. Not retried: the winner enabled the factor, and a factor is enabled only once.
        var saveResult = await repository.UpdateAsync(user, user.Id, cancellationToken);
        if (saveResult.IsFailed) return Result.Fail(UserErrors.MfaCodeInvalid());

        return Result.Ok(new CompletedTotpEnrollment(rawCodes));
    }
}
