using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Features.Authorize.BeginTotpEnrollment;
using WardenId.Protocol.Domain;
using IdentityBeginTotpReplacementCommand = WardenId.Identity.Integration.Command.Features.Users.BeginTotpReplacement.Command;

namespace WardenId.Protocol.Application.Features.Account.BeginTotpReplacement;

/// <summary>Any failure is answered like a wrong code; Identity counts wrong codes towards lockout.</summary>
public sealed class Handler(ISender sender) : IRequestHandler<Command, Result<StartedTotpEnrollment>>
{
    public async Task<Result<StartedTotpEnrollment>> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new IdentityBeginTotpReplacementCommand(request.TenantId, request.UserId, request.Code, request.Issuer),
            cancellationToken);
        return result.IsSuccess
            ? Result.Ok(new StartedTotpEnrollment(result.Value.FactorId, result.Value.Secret, result.Value.OtpAuthUri))
            : Result.Fail(ProtocolErrors.InvalidSecondFactor());
    }
}
