using MediatR;
using WardenId.Protocol.Domain.Sessions;
using EndForUserCommand = WardenId.Protocol.Application.Features.Sessions.EndForUser.Command;

namespace WardenId.Protocol.Application.Features.Account;

/// <summary>
/// Ends the user's other sessions in the tenant after an authenticator change, keeping the one the change was made
/// from. Same path as every session end: each ended session raises SessionEndedDomainEvent, whose subscriber revokes
/// its grants (not offline_access ones, as for a password change), sends the back-channel logouts and audits the end
/// with reason <see cref="EndReason.MfaChanged"/>.
/// </summary>
internal static class OtherSessions
{
    public static async Task EndAsync(
        ISender sender, Guid tenantId, Guid userId, Guid currentSid, CancellationToken cancellationToken)
    {
        // The change is already saved: don't let a client hanging up skip the session ends.
        var result = await sender.Send(
            new EndForUserCommand(userId, EndReason.MfaChanged, tenantId, ExceptSid: currentSid), CancellationToken.None);
        if (result.IsFailed)
            throw new InvalidOperationException(
                $"Ending the other sessions of user {userId} failed: {string.Join("; ", result.Errors.Select(e => e.Message))}");
    }
}
