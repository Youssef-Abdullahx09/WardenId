using FluentResults;
using MediatR;
using WardenId.Protocol.Domain;
using IdentityCompleteTotpEnrollmentCommand = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.Command;

namespace WardenId.Protocol.Application.Features.Account.CompleteTotpSetup;

/// <summary>
/// Unlike the sign-in enrollment step, a wrong code here is not a failed sign-in: nothing is audited (the audit
/// catalog has no account step-up event), the user gets the same message as for any wrong code.
/// </summary>
public sealed class Handler(ISender sender) : IRequestHandler<Command, Result<IReadOnlyList<string>>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new IdentityCompleteTotpEnrollmentCommand(request.TenantId, request.UserId, request.FactorId, request.Code),
            cancellationToken);
        return result.IsSuccess
            ? Result.Ok(result.Value.RecoveryCodes)
            : Result.Fail(ProtocolErrors.InvalidSecondFactor());
    }
}
