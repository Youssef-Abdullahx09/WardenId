using FluentResults;
using MediatR;
using IdentityBeginTotpEnrollmentCommand = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.Command;

namespace WardenId.Protocol.Application.Features.Authorize.BeginTotpEnrollment;

public sealed class Handler(ISender sender) : IRequestHandler<Command, Result<StartedTotpEnrollment>>
{
    public async Task<Result<StartedTotpEnrollment>> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new IdentityBeginTotpEnrollmentCommand(request.TenantId, request.UserId, request.Issuer), cancellationToken);
        return result.IsSuccess
            ? Result.Ok(new StartedTotpEnrollment(result.Value.FactorId, result.Value.Secret, result.Value.OtpAuthUri))
            : Result.Fail(result.Errors);
    }
}
