using FluentResults;
using MediatR;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using WardenId.Protocol.Domain;
using IdentityRemoveMfaCommand = WardenId.Identity.Integration.Command.Features.Users.RemoveMfa.Command;

namespace WardenId.Protocol.Application.Features.Account.RemoveMfa;

/// <summary>
/// A failed code check (tagged by Identity) gets the one wrong-code message; any other refusal, such as a policy
/// that requires MFA, gets a fixed message that reveals nothing about the code.
/// </summary>
public sealed class Handler(ISender sender) : IRequestHandler<Command, Result>
{
    public const string RefusedMessage = "Two-step verification can't be turned off for your account.";

    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new IdentityRemoveMfaCommand(request.TenantId, request.UserId, request.Code), cancellationToken);
        if (result.IsSuccess) return Result.Ok();

        return Result.Fail(SecondFactorFailure.Read(result.Errors) is null
            ? ProtocolErrors.Validation(RefusedMessage)
            : ProtocolErrors.InvalidSecondFactor());
    }
}
