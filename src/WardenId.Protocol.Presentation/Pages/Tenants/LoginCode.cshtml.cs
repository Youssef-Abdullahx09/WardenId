using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardenId.Protocol.Application.Features.Authorize.SignIn;
using WardenId.Protocol.Application.Features.Policies;
using VerifySecondFactorCommand = WardenId.Protocol.Application.Features.Authorize.VerifySecondFactor.Command;

namespace WardenId.Protocol.Presentation.Pages.Tenants;

/// <summary>The code step: a code from the user's authenticator app, or one of their recovery codes.</summary>
public sealed class LoginCodeModel(ISender sender, ITenantLifetimes tenantLifetimes)
    : SignInStepPageModel(sender, tenantLifetimes)
{
    /// <summary>Only changes the label and keyboard; either kind of code is accepted either way.</summary>
    [BindProperty(SupportsGet = true)] public bool UseRecoveryCode { get; set; }

    [BindProperty] public string Code { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var (_, elsewhere) = await LoadTicketAsync(slug, SignInStep.SecondFactor, cancellationToken);
        return elsewhere ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug, CancellationToken cancellationToken)
    {
        var (ticket, elsewhere) = await LoadTicketAsync(slug, SignInStep.SecondFactor, cancellationToken);
        if (ticket is null) return elsewhere!;

        var result = await Sender.Send(
            new VerifySecondFactorCommand(ticket.TenantId, ticket.UserId, Code.Trim()), cancellationToken);
        Code = string.Empty;
        if (result.IsFailed)
        {
            ErrorMessage = result.Errors[0].Message;
            return Page();
        }

        return await ContinueAsync(ticket, result.Value.Amr, cancellationToken) ?? AfterSignIn();
    }
}
