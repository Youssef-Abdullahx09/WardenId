using Microsoft.Extensions.Time.Testing;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using BeginCommand = WardenId.Protocol.Application.Features.Authorize.BeginTotpEnrollment.Command;
using BeginHandler = WardenId.Protocol.Application.Features.Authorize.BeginTotpEnrollment.Handler;
using CompleteCommand = WardenId.Protocol.Application.Features.Authorize.CompleteTotpEnrollment.Command;
using CompleteHandler = WardenId.Protocol.Application.Features.Authorize.CompleteTotpEnrollment.Handler;
using IdentityBeginCommand = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.Command;
using IdentityCompleteCommand = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.Command;
using IdentityCompleted = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.CompletedTotpEnrollment;
using IdentityEnrollment = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.TotpEnrollment;

namespace WardenId.Protocol.Application.Tests.Features.Authorize.TotpEnrollment;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid FactorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-16T08:00:00Z");

    [Fact]
    public async Task Begin_PassesTheIssuer_AndReturnsTheSecret()
    {
        var sender = new StubSender().Returns<IdentityBeginCommand, Result<IdentityEnrollment>>(
            Result.Ok(new IdentityEnrollment(FactorId, "JBSWY3DPEHPK3PXP", "otpauth://totp/acme:alice?secret=JBSWY3DPEHPK3PXP")));

        var result = await new BeginHandler(sender)
            .Handle(new BeginCommand(TenantId, UserId, "acme"), CancellationToken.None);

        Assert.Equal(FactorId, result.Value.FactorId);
        Assert.Equal("JBSWY3DPEHPK3PXP", result.Value.Secret);
        Assert.Equal("acme", Assert.IsType<IdentityBeginCommand>(Assert.Single(sender.Sent)).Issuer);
    }

    [Theory]
    [InlineData(false, SecurityEvent.TotpEnrolled)]
    [InlineData(true, SecurityEvent.AuthenticatorReplaced)]
    public async Task Complete_ReturnsTheRecoveryCodes_AndAuditsTheEnrollment(bool replaced, SecurityEvent expected)
    {
        var sender = new StubSender().Returns<IdentityCompleteCommand, Result<IdentityCompleted>>(
            Result.Ok(new IdentityCompleted(["abcde-fghij", "klmno-pqrst"], replaced)));
        var audit = new FakeAuthAuditTrail();

        var result = await new CompleteHandler(sender, audit, new FakeTimeProvider(Now))
            .Handle(new CompleteCommand(TenantId, UserId, FactorId, "123456"), CancellationToken.None);

        Assert.Equal(["abcde-fghij", "klmno-pqrst"], result.Value.RecoveryCodes);
        Assert.Equal(FactorId, Assert.IsType<IdentityCompleteCommand>(Assert.Single(sender.Sent)).FactorId);
        var recorded = Assert.Single(audit.Recorded);
        Assert.Equal(expected, recorded.Event);
        Assert.Equal(TenantId, recorded.TenantId);
        Assert.Equal(Now, recorded.OccurredAt);
        Assert.Equal(Actor.User(UserId), recorded.Actor);
        Assert.Equal([Target.User(UserId)], recorded.Targets);
        Assert.Null(recorded.Reason);
        Assert.Null(recorded.Session);
    }

    [Fact]
    public async Task Complete_WrongCode_IsAuditedAsInvalidSecondFactor_AndAnsweredGenerically()
    {
        var sender = new StubSender().Returns<IdentityCompleteCommand, Result<IdentityCompleted>>(
            Result.Fail<IdentityCompleted>("Invalid code."));
        var audit = new FakeAuthAuditTrail();

        var result = await new CompleteHandler(sender, audit, new FakeTimeProvider(Now))
            .Handle(new CompleteCommand(TenantId, UserId, FactorId, "000000"), CancellationToken.None);

        Assert.Equal("That code didn't work. Please try again.", result.Errors.Single().Message);
        var recorded = Assert.Single(audit.Recorded);
        Assert.Equal(SecurityEvent.SignInFailed, recorded.Event);
        Assert.Equal(OutcomeReason.InvalidSecondFactor, recorded.Reason);
        Assert.Equal(Actor.User(UserId), recorded.Actor);
    }
}
