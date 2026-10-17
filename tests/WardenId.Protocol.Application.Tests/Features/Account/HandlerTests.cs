using WardenId.Identity.Integration.Command.Features.Users.GetAccount;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using AcceptTermsCommand = WardenId.Protocol.Application.Features.Authorize.AcceptTerms.Command;
using AcceptTermsHandler = WardenId.Protocol.Application.Features.Authorize.AcceptTerms.Handler;
using BeginReplacementCommand = WardenId.Protocol.Application.Features.Account.BeginTotpReplacement.Command;
using BeginReplacementHandler = WardenId.Protocol.Application.Features.Account.BeginTotpReplacement.Handler;
using ChangePasswordCommand = WardenId.Protocol.Application.Features.Account.ChangePassword.Command;
using ChangePasswordHandler = WardenId.Protocol.Application.Features.Account.ChangePassword.Handler;
using CompleteSetupCommand = WardenId.Protocol.Application.Features.Account.CompleteTotpSetup.Command;
using CompleteSetupHandler = WardenId.Protocol.Application.Features.Account.CompleteTotpSetup.Handler;
using GetHandler = WardenId.Protocol.Application.Features.Account.Get.Handler;
using GetQuery = WardenId.Protocol.Application.Features.Account.Get.Query;
using IdentityAcceptTermsCommand = WardenId.Identity.Integration.Command.Features.Users.AcceptTerms.Command;
using IdentityBeginReplacementCommand = WardenId.Identity.Integration.Command.Features.Users.BeginTotpReplacement.Command;
using IdentityChangeOwnPasswordCommand = WardenId.Identity.Integration.Command.Features.Users.ChangeOwnPassword.Command;
using IdentityCompleteCommand = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.Command;
using IdentityCompleted = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.CompletedTotpEnrollment;
using IdentityEnrollment = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.TotpEnrollment;
using IdentityGetAccountQuery = WardenId.Identity.Integration.Command.Features.Users.GetAccount.Query;
using IdentityRegenerateCommand = WardenId.Identity.Integration.Command.Features.Users.RegenerateRecoveryCodes.Command;
using IdentityRemoveMfaCommand = WardenId.Identity.Integration.Command.Features.Users.RemoveMfa.Command;
using RegenerateCommand = WardenId.Protocol.Application.Features.Account.RegenerateRecoveryCodes.Command;
using RegenerateHandler = WardenId.Protocol.Application.Features.Account.RegenerateRecoveryCodes.Handler;
using RemoveMfaCommand = WardenId.Protocol.Application.Features.Account.RemoveMfa.Command;
using RemoveMfaHandler = WardenId.Protocol.Application.Features.Account.RemoveMfa.Handler;

namespace WardenId.Protocol.Application.Tests.Features.Account;

/// <summary>The account and terms handlers: thin over Identity, but they decide what the user is told.</summary>
public class HandlerTests
{
    private const string WrongCode = "That code didn't work. Please try again.";

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid FactorId = Guid.NewGuid();
    private static readonly DateTimeOffset Enrolled = DateTimeOffset.Parse("2026-10-01T08:00:00Z");

    private static Error Tagged(SecondFactorFailureReason reason) =>
        SecondFactorFailure.Tag(new Error("Invalid code."), reason);

    private static AccountView View(string? termsUrl) =>
        new("alice@acme.test", "alice", null, termsUrl, MfaAvailable: true, MfaRequired: false,
            [new AccountFactor(FactorId, "Totp", Enrolled, null)], 7);

    [Fact]
    public async Task Get_MapsTheAccount_ForTheGivenUser()
    {
        var sender = new StubSender().Returns<IdentityGetAccountQuery, Result<AccountView>>(
            Result.Ok(View("https://acme.example/terms")));

        var result = await new GetHandler(sender).Handle(new GetQuery(TenantId, UserId), CancellationToken.None);

        Assert.Equal("alice", result.Value.Username);
        Assert.Equal("https://acme.example/terms", result.Value.TermsUrl);
        Assert.True(result.Value.HasMfa);
        Assert.Equal(7, result.Value.RemainingRecoveryCodes);
        Assert.Equal(Enrolled, Assert.Single(result.Value.Factors).EnrolledAtUtc);
        var sent = Assert.IsType<IdentityGetAccountQuery>(Assert.Single(sender.Sent));
        Assert.Equal((TenantId, UserId), (sent.TenantId, sent.UserId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("/relative/terms")]
    [InlineData("ftp://acme.example/terms")]
    public async Task Get_DropsATermsUrlThatIsNotAWebLink(string? termsUrl)
    {
        var sender = new StubSender().Returns<IdentityGetAccountQuery, Result<AccountView>>(Result.Ok(View(termsUrl)));

        var result = await new GetHandler(sender).Handle(new GetQuery(TenantId, UserId), CancellationToken.None);

        Assert.Null(result.Value.TermsUrl);
    }

    [Fact]
    public async Task AcceptTerms_PassesTheUser_AndAnswersFailuresGenerically()
    {
        var ok = new StubSender().Returns<IdentityAcceptTermsCommand, Result>(Result.Ok());
        Assert.True((await new AcceptTermsHandler(ok).Handle(new AcceptTermsCommand(TenantId, UserId), CancellationToken.None)).IsSuccess);
        Assert.Equal(UserId, Assert.IsType<IdentityAcceptTermsCommand>(Assert.Single(ok.Sent)).UserId);

        var failing = new StubSender().Returns<IdentityAcceptTermsCommand, Result>(Result.Fail("db down: secret detail"));
        var result = await new AcceptTermsHandler(failing).Handle(new AcceptTermsCommand(TenantId, UserId), CancellationToken.None);
        Assert.DoesNotContain("secret detail", result.Errors[0].Message);
    }

    [Fact]
    public async Task ChangePassword_PassesIdentitysMessagesThrough()
    {
        var sender = new StubSender().Returns<IdentityChangeOwnPasswordCommand, Result>(
            Result.Fail("Password must be at least 12 characters."));

        var result = await new ChangePasswordHandler(sender)
            .Handle(new ChangePasswordCommand(TenantId, UserId, "old", "new"), CancellationToken.None);

        Assert.Equal("Password must be at least 12 characters.", result.Errors[0].Message);
        var sent = Assert.IsType<IdentityChangeOwnPasswordCommand>(Assert.Single(sender.Sent));
        Assert.Equal(("old", "new"), (sent.CurrentPassword, sent.NewPassword));
    }

    [Fact]
    public async Task CompleteSetup_ReturnsTheCodes()
    {
        var sender = new StubSender().Returns<IdentityCompleteCommand, Result<IdentityCompleted>>(
            Result.Ok(new IdentityCompleted(["abcde-fghij"])));

        var result = await new CompleteSetupHandler(sender)
            .Handle(new CompleteSetupCommand(TenantId, UserId, FactorId, "123456"), CancellationToken.None);

        Assert.Equal(["abcde-fghij"], result.Value);
        Assert.Equal(FactorId, Assert.IsType<IdentityCompleteCommand>(Assert.Single(sender.Sent)).FactorId);
    }

    [Fact]
    public async Task CompleteSetup_WrongCode_IsAnsweredGenerically()
    {
        var sender = new StubSender().Returns<IdentityCompleteCommand, Result<IdentityCompleted>>(
            Result.Fail<IdentityCompleted>("Invalid code."));

        var result = await new CompleteSetupHandler(sender)
            .Handle(new CompleteSetupCommand(TenantId, UserId, FactorId, "000000"), CancellationToken.None);

        Assert.Equal(WrongCode, result.Errors.Single().Message);
    }

    [Fact]
    public async Task BeginReplacement_PassesCodeAndIssuer_AndReturnsTheNewSecret()
    {
        var sender = new StubSender().Returns<IdentityBeginReplacementCommand, Result<IdentityEnrollment>>(
            Result.Ok(new IdentityEnrollment(FactorId, "JBSWY3DPEHPK3PXP", "otpauth://totp/acme:alice")));

        var result = await new BeginReplacementHandler(sender)
            .Handle(new BeginReplacementCommand(TenantId, UserId, "123456", "acme"), CancellationToken.None);

        Assert.Equal(FactorId, result.Value.FactorId);
        var sent = Assert.IsType<IdentityBeginReplacementCommand>(Assert.Single(sender.Sent));
        Assert.Equal(("123456", "acme"), (sent.Code, sent.Issuer));
    }

    [Theory]
    [InlineData(SecondFactorFailureReason.InvalidCode)]
    [InlineData(SecondFactorFailureReason.LockedOut)]
    [InlineData(SecondFactorFailureReason.AccountInactive)]
    public async Task StepUpFailures_AreAllAnsweredWithTheSameMessage(SecondFactorFailureReason reason)
    {
        var sender = new StubSender()
            .Returns<IdentityBeginReplacementCommand, Result<IdentityEnrollment>>(Result.Fail<IdentityEnrollment>(Tagged(reason)))
            .Returns<IdentityRegenerateCommand, Result<IReadOnlyList<string>>>(Result.Fail<IReadOnlyList<string>>(Tagged(reason)))
            .Returns<IdentityRemoveMfaCommand, Result>(Result.Fail(Tagged(reason)));

        var replace = await new BeginReplacementHandler(sender)
            .Handle(new BeginReplacementCommand(TenantId, UserId, "000000", "acme"), CancellationToken.None);
        var regenerate = await new RegenerateHandler(sender)
            .Handle(new RegenerateCommand(TenantId, UserId, "000000"), CancellationToken.None);
        var remove = await new RemoveMfaHandler(sender)
            .Handle(new RemoveMfaCommand(TenantId, UserId, "000000"), CancellationToken.None);

        Assert.Equal(WrongCode, replace.Errors.Single().Message);
        Assert.Equal(WrongCode, regenerate.Errors.Single().Message);
        Assert.Equal(WrongCode, remove.Errors.Single().Message);
    }

    [Fact]
    public async Task Regenerate_ReturnsTheNewCodes()
    {
        var sender = new StubSender().Returns<IdentityRegenerateCommand, Result<IReadOnlyList<string>>>(
            Result.Ok<IReadOnlyList<string>>(["one", "two"]));

        var result = await new RegenerateHandler(sender)
            .Handle(new RegenerateCommand(TenantId, UserId, "123456"), CancellationToken.None);

        Assert.Equal(["one", "two"], result.Value);
    }

    [Fact]
    public async Task RemoveMfa_RefusedByPolicy_GetsAFixedMessage()
    {
        var sender = new StubSender().Returns<IdentityRemoveMfaCommand, Result>(
            Result.Fail("The tenant's MFA policy requires MFA for this user."));

        var result = await new RemoveMfaHandler(sender)
            .Handle(new RemoveMfaCommand(TenantId, UserId, "123456"), CancellationToken.None);

        Assert.Equal(RemoveMfaHandler.RefusedMessage, result.Errors.Single().Message);
    }
}
