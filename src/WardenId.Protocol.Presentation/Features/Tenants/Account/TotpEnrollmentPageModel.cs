using MediatR;
using Microsoft.AspNetCore.Mvc;
using CompleteTotpSetupCommand = WardenId.Protocol.Application.Features.Account.CompleteTotpSetup.Command;

namespace WardenId.Protocol.Presentation.Features.Tenants.Account;

/// <summary>
/// Setting up an authenticator app from the account page (first one, or a replacement): QR code and secret, the
/// first code, then the recovery codes, shown once. Between requests the factor id and secret live only in the
/// protected <see cref="State"/> (<see cref="AccountEnrollment"/>), bound to this tenant, this user and this kind.
/// </summary>
public abstract class TotpEnrollmentPageModel(ISender sender) : AccountPageModel(sender)
{
    private const string Expired = "Setting up your authenticator app took too long. Please start again.";

    [BindProperty] public string Code { get; set; } = string.Empty;

    /// <summary>The protected enrollment state, posted back with the code.</summary>
    [BindProperty] public string? State { get; set; }

    public TotpEnrollmentView? Enrollment { get; private set; }

    /// <summary>Set once the new factor is enabled: shown on this response only.</summary>
    public IReadOnlyList<string>? RecoveryCodes { get; private set; }

    protected abstract AccountEnrollmentKind Kind { get; }

    /// <summary>The factor Identity just started: show it, with its state protected for the next post.</summary>
    protected IActionResult ShowEnrollment(PendingTotpEnrollment enrollment)
    {
        State = AccountEnrollment.Protect(
            HttpContext, AccountEnrollment.Create(HttpContext, TenantId, UserId, Kind, enrollment));
        Enrollment = TotpEnrollmentView.For(enrollment, State);
        return Page();
    }

    public async Task<IActionResult> OnPostVerifyAsync(string slug, CancellationToken cancellationToken)
    {
        if (await LoadAsync(slug, cancellationToken) is { } elsewhere) return elsewhere;

        var state = AccountEnrollment.Read(HttpContext, State, TenantId, UserId, Kind);
        var code = Code.Trim();
        Code = string.Empty;
        if (state is null)
        {
            State = null;
            ErrorMessage = Expired;
            return Page();
        }

        var result = await Sender.Send(
            new CompleteTotpSetupCommand(TenantId, UserId, Sid, state.FactorId, code), cancellationToken);
        if (result.IsFailed)
        {
            ErrorMessage = WrongCode;
            Enrollment = TotpEnrollmentView.For(state.Enrollment, State);
            return Page();
        }

        State = null;
        RecoveryCodes = result.Value;
        return Page();
    }
}
