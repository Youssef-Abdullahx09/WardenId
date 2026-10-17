using WardenId.Identity.Application.Features.Users.Get;

namespace WardenId.Identity.Application.Tests.Features.Users.Get;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsUserDetail_WhenUserExists()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com", "someone");
        user.MarkAsCreated(Guid.NewGuid());
        var acceptedAt = new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
        user.AcceptTerms(acceptedAt);

        using (var seedContext = TestIdentityDbContext.CreateNew(databaseName))
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("someone@example.com", result.Value.Email);
        Assert.Equal("someone", result.Value.Username);
        Assert.Equal(UserStatus.PendingVerification, result.Value.Status);
        Assert.Contains(RequiredAction.VerifyEmail, result.Value.PendingActions);
        Assert.Equal(acceptedAt, result.Value.TermsAcceptedAtUtc);
    }

    // Regression: the query had no .Include(...), so these two navigations always came back empty.
    [Fact]
    public async Task Handle_ReturnsAttributesAndMfaFactors()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com", "someone");
        user.MarkAsCreated(Guid.NewGuid());
        user.SetAttribute("department", "engineering", UserAttributeValueType.String);
        var factor = user.AddEnabledTotp(recoveryCodes: [RecoveryCode.Create("hash-1"), RecoveryCode.Create("hash-2")]);
        user.ConsumeRecoveryCode("hash-1", TestMfa.Now);
        var pending = MfaFactor.Create(MfaFactorType.Totp, "protected:", TestMfa.Now);
        user.BeginMfaEnrollment(pending);

        using (var seedContext = TestIdentityDbContext.CreateNew(databaseName))
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, user.Id), CancellationToken.None);

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
    public async Task Handle_Fails_WhenUserDoesNotExist()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserBelongsToADifferentTenant()
    {
        var databaseName = Guid.NewGuid().ToString();
        var user = TestUsers.CreatePendingVerification(Guid.NewGuid());
        using (var seedContext = TestIdentityDbContext.CreateNew(databaseName))
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(Guid.NewGuid(), user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserIsDeleted()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.MarkAsDeleted(Guid.NewGuid());

        using (var seedContext = TestIdentityDbContext.CreateNew(databaseName))
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
