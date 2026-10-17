using FluentResults;
using MediatR;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Domain;
using IdentityVerifySecondFactorCommand = WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor.Command;

namespace WardenId.Protocol.Application.Features.Authorize.VerifySecondFactor;

/// <summary>
/// Checks the code with Identity (which counts wrong codes towards lockout). Every failure is audited as a failed
/// sign-in with its real reason; the user always gets the same message.
/// </summary>
public sealed class Handler(ISender sender, IAuthAuditTrail audit, TimeProvider timeProvider)
    : IRequestHandler<Command, Result<SecondFactorPassed>>
{
    public async Task<Result<SecondFactorPassed>> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new IdentityVerifySecondFactorCommand(request.TenantId, request.UserId, request.Code), cancellationToken);
        if (result.IsSuccess)
            return Result.Ok(new SecondFactorPassed(result.Value.Method == SecondFactorMethod.RecoveryCode
                ? AuthenticationMethods.PasswordAndRecoveryCode
                : AuthenticationMethods.PasswordAndTotp));

        var reason = await SecondFactorAuditReason.OfAsync(
                sender, request.TenantId, request.UserId, result.Errors, cancellationToken)
            ?? OutcomeReason.InvalidCredentials;
        await audit.RecordAsync(SignInAudit.Failed(request.TenantId, request.UserId, reason, timeProvider.GetUtcNow()));
        return Result.Fail(ProtocolErrors.InvalidSecondFactor());
    }
}
