using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardenId.Protocol.Application.Features.Authorize.SignIn;
using WardenId.Protocol.Application.Features.Policies;
using AcceptTermsCommand = WardenId.Protocol.Application.Features.Authorize.AcceptTerms.Command;
using GetAccountQuery = WardenId.Protocol.Application.Features.Account.Get.Query;

namespace WardenId.Protocol.Presentation.Pages.Tenants;

/// <summary>
/// The terms step (the AcceptTos required action): a link to the tenant's terms and a required "I accept". How the
/// user authenticated so far is unchanged by it.
/// </summary>
public sealed class LoginTermsModel(ISender sender, ITenantLifetimes tenantLifetimes)
    : SignInStepPageModel(sender, tenantLifetimes)
{
    [BindProperty] public bool Accept { get; set; }

    /// <summary>An absolute http(s) link, or null: the page then asks for acceptance without a link.</summary>
    public string? TermsUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var (ticket, elsewhere) = await LoadTicketAsync(slug, SignInStep.AcceptTerms, cancellationToken);
        if (ticket is null) return elsewhere!;

        await LoadTermsUrlAsync(ticket, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug, CancellationToken cancellationToken)
    {
        var (ticket, elsewhere) = await LoadTicketAsync(slug, SignInStep.AcceptTerms, cancellationToken);
        if (ticket is null) return elsewhere!;

        if (!Accept)
        {
            ErrorMessage = "Please accept the terms of service to continue.";
            await LoadTermsUrlAsync(ticket, cancellationToken);
            return Page();
        }

        var result = await Sender.Send(new AcceptTermsCommand(ticket.TenantId, ticket.UserId), cancellationToken);
        if (result.IsFailed)
        {
            ErrorMessage = result.Errors[0].Message;
            await LoadTermsUrlAsync(ticket, cancellationToken);
            return Page();
        }

        return await ContinueAsync(ticket, ticket.Amr, cancellationToken) ?? AfterSignIn();
    }

    private async Task LoadTermsUrlAsync(PendingSignInTicket ticket, CancellationToken cancellationToken)
    {
        var account = await Sender.Send(new GetAccountQuery(ticket.TenantId, ticket.UserId), cancellationToken);
        TermsUrl = account.IsSuccess ? account.Value.TermsUrl : null;
    }
}
