using WardenId.Identity.Application.Features.Users.Get;
using GetQuery = WardenId.Identity.Application.Features.Users.Get.Query;

namespace WardenId.Identity.Application.Tests.Features.Users.Get;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsUserDetail_WhenUserExists()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com", "someone");
        var acceptedAt = new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
        user.AcceptTerms(acceptedAt);
        var handler = new Handler(new FakeUsersQuery().With(user));

        var result = await handler.Handle(new GetQuery(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.Id);
        Assert.Equal("someone@example.com", result.Value.Email);
        Assert.Equal("someone", result.Value.Username);
        Assert.Equal(UserStatus.PendingVerification, result.Value.Status);
        Assert.Contains(RequiredAction.VerifyEmail, result.Value.PendingActions);
        Assert.Equal(acceptedAt, result.Value.TermsAcceptedAtUtc);
        Assert.Equal(TimeSpan.Zero, result.Value.TermsAcceptedAtUtc!.Value.Offset);
    }

    [Fact]
    public async Task Handle_ReturnsAttributesAndMfaFactors()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com", "someone");
        user.SetAttribute("department", "engineering", UserAttributeValueType.String);
        var factor = user.AddEnabledTotp(recoveryCodes: [RecoveryCode.Create("hash-1"), RecoveryCode.Create("hash-2")]);
        user.ConsumeRecoveryCode("hash-1", TestMfa.Now);
        var pending = MfaFactor.Create(MfaFactorType.Totp, "protected:", TestMfa.Now);
        user.BeginMfaEnrollment(pending);
        var handler = new Handler(new FakeUsersQuery().With(user));

        var result = await handler.Handle(new GetQuery(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var attribute = Assert.Single(result.Value.Attributes);
        Assert.Equal("department", attribute.Key);
        Assert.Equal("engineering", attribute.Value);
        Assert.Equal(UserAttributeValueType.String, attribute.ValueType);
        Assert.Equal([factor.Id, pending.Id], result.Value.MfaFactors.Select(f => f.Id));
        var mfaFactor = result.Value.MfaFactors.First();
        Assert.Equal(MfaFactorType.Totp, mfaFactor.FactorType);
        Assert.True(mfaFactor.Enabled);
        Assert.Equal(factor.EnrolledAtUtc, mfaFactor.EnrolledAtUtc);
        Assert.Equal(factor.VerifiedAtUtc, mfaFactor.VerifiedAtUtc);
        Assert.Equal(factor.LastUsedAtUtc, mfaFactor.LastUsedAtUtc);
        var pendingSummary = result.Value.MfaFactors.Last();
        Assert.False(pendingSummary.Enabled);
        Assert.Null(pendingSummary.VerifiedAtUtc);
        Assert.Equal(1, result.Value.RemainingRecoveryCodes);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTheQueryFindsNoUser()
    {
        var handler = new Handler(new FakeUsersQuery());

        var result = await handler.Handle(new GetQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.NotFound, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserBelongsToADifferentTenant()
    {
        var user = TestUsers.CreatePendingVerification(Guid.NewGuid());
        var handler = new Handler(new FakeUsersQuery().With(user));

        var result = await handler.Handle(new GetQuery(Guid.NewGuid(), user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserIsDeleted()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.SoftDelete();
        var handler = new Handler(new FakeUsersQuery().With(user));

        var result = await handler.Handle(new GetQuery(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_PassesTheQueryLayersFailureThrough()
    {
        var handler = new Handler(new FakeUsersQuery { Failure = new Error("sql file missing") });

        var result = await handler.Handle(new GetQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("sql file missing", result.Errors[0].Message);
    }
}
