using FluentResults;
using MediatR;
using WardenId.Protocol.Domain;
using IdentityRegenerateRecoveryCodesCommand = WardenId.Identity.Integration.Command.Features.Users.RegenerateRecoveryCodes.Command;

namespace WardenId.Protocol.Application.Features.Account.RegenerateRecoveryCodes;

/// <summary>Any failure is answered like a wrong code; Identity counts wrong codes towards lockout.</summary>
public sealed class Handler(ISender sender) : IRequestHandler<Command, Result<IReadOnlyList<string>>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new IdentityRegenerateRecoveryCodesCommand(request.TenantId, request.UserId, request.Code), cancellationToken);
        return result.IsSuccess ? Result.Ok(result.Value) : Result.Fail(ProtocolErrors.InvalidSecondFactor());
    }
}
