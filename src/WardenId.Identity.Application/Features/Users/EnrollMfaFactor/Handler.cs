using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.EnrollMfaFactor;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result<MfaFactorEnrolled>>
{
    private const int RecoveryCodeCount = 10;

    public async Task<Result<MfaFactorEnrolled>> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;
        var factor = MfaFactor.Create(request.FactorType, request.CredentialData);

        var rawCodes = new List<string>(RecoveryCodeCount);
        var recoveryCodes = new List<RecoveryCode>(RecoveryCodeCount);
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var (raw, hash) = SecureTokenGenerator.Generate();
            rawCodes.Add(raw);
            recoveryCodes.Add(RecoveryCode.Create(hash));
        }

        var enrollResult = user.EnrollMfaFactor(factor, recoveryCodes);
        if (enrollResult.IsFailed) return Result.Fail(enrollResult.Errors);

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok(new MfaFactorEnrolled(factor.Id, rawCodes));
    }
}
