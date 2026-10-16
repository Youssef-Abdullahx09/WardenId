using WardenId.Tenancy.Domain.Events;

namespace WardenId.Tenancy.Application.Tests.Domain;

public class TenantSigningKeyRotationTests
{
    private static SigningKey NewKey()
    {
        var keyId = Guid.NewGuid();
        return SigningKey.CreateEs256(keyId, TenantBuilder.Jwk(keyId), [7, 8, 9]);
    }

    [Fact]
    public void NewKey_IsActive_AndActivatedNow()
    {
        var before = DateTimeOffset.UtcNow;

        var key = NewKey();

        Assert.Equal(SigningKeyStatus.Active, key.Status);
        Assert.True(key.IsActive);
        Assert.True(key.IsPublished);
        Assert.InRange(key.ActivatedAt, before, DateTimeOffset.UtcNow);
        Assert.Null(key.RetiringSince);
        Assert.Null(key.RetiredAt);
    }

    [Theory]
    [InlineData(Status.Provisioning)]
    [InlineData(Status.Active)]
    [InlineData(Status.Suspended)]
    public void Rotate_MakesTheNewKeyActive_AndThePreviousKeyRetiring(Status status)
    {
        var (tenant, previous) = TenantBuilder.Create("acme", status);
        tenant.ClearDomainEvents();
        var next = NewKey();

        var result = tenant.RotateSigningKey(next);

        Assert.True(result.IsSuccess);
        Assert.Equal(SigningKeyStatus.Active, next.Status);
        Assert.Equal(SigningKeyStatus.Retiring, previous.Status);
        Assert.False(previous.IsActive);
        Assert.True(previous.IsPublished);
        Assert.NotNull(previous.RetiringSince);
        Assert.Null(previous.RetiredAt);
        Assert.Single(tenant.SigningKeys, k => k.IsActive);
        var raised = Assert.IsType<TenantSigningKeyRotatedDomainEvent>(Assert.Single(tenant.DomainEvents));
        Assert.Equal(next.SigningKeyId, raised.SigningKeyId);
        Assert.Equal(previous.SigningKeyId, raised.PreviousSigningKeyId);
    }

    [Fact]
    public void Rotate_RefusesAnArchivedTenant()
    {
        var (tenant, previous) = TenantBuilder.Create("acme", Status.Archived);

        var result = tenant.RotateSigningKey(NewKey());

        Assert.True(result.IsFailed);
        Assert.Equal(SigningKeyStatus.Active, previous.Status);
        Assert.Single(tenant.SigningKeys);
    }

    [Fact]
    public void Rotate_RefusesAKeyAlreadyOnTheTenant()
    {
        var (tenant, previous) = TenantBuilder.Create("acme", Status.Active);

        var result = tenant.RotateSigningKey(previous);

        Assert.True(result.IsFailed);
        Assert.Equal(SigningKeyStatus.Active, previous.Status);
    }

    [Fact]
    public void Rotate_Twice_LeavesBothOlderKeysRetiring()
    {
        var (tenant, first) = TenantBuilder.Create("acme", Status.Active);
        var second = NewKey();
        var third = NewKey();

        Assert.True(tenant.RotateSigningKey(second).IsSuccess);
        Assert.True(tenant.RotateSigningKey(third).IsSuccess);

        Assert.Equal(SigningKeyStatus.Retiring, first.Status);
        Assert.Equal(SigningKeyStatus.Retiring, second.Status);
        Assert.Equal(SigningKeyStatus.Active, third.Status);
    }

    [Fact]
    public void Retire_MovesARetiringKeyToRetired()
    {
        var (tenant, previous) = TenantBuilder.Create("acme", Status.Active);
        Assert.True(tenant.RotateSigningKey(NewKey()).IsSuccess);
        tenant.ClearDomainEvents();

        var result = tenant.RetireSigningKey(previous.SigningKeyId);

        Assert.True(result.IsSuccess);
        Assert.Equal(SigningKeyStatus.Retired, previous.Status);
        Assert.False(previous.IsPublished);
        Assert.NotNull(previous.RetiredAt);
        var raised = Assert.IsType<TenantSigningKeyRetiredDomainEvent>(Assert.Single(tenant.DomainEvents));
        Assert.Equal(previous.SigningKeyId, raised.SigningKeyId);
    }

    [Fact]
    public void Retire_RefusesTheActiveKey()
    {
        var (tenant, key) = TenantBuilder.Create("acme", Status.Active);

        var result = tenant.RetireSigningKey(key.SigningKeyId);

        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.Validation, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
        Assert.Equal(SigningKeyStatus.Active, key.Status);
    }

    [Fact]
    public void Retire_RefusesAnAlreadyRetiredKey()
    {
        var (tenant, previous) = TenantBuilder.Create("acme", Status.Active);
        Assert.True(tenant.RotateSigningKey(NewKey()).IsSuccess);
        Assert.True(tenant.RetireSigningKey(previous.SigningKeyId).IsSuccess);
        var retiredAt = previous.RetiredAt;

        var result = tenant.RetireSigningKey(previous.SigningKeyId);

        Assert.True(result.IsFailed);
        Assert.Equal(retiredAt, previous.RetiredAt);
    }

    [Fact]
    public void Retire_ReturnsNotFound_ForAnUnknownKey()
    {
        var (tenant, _) = TenantBuilder.Create("acme", Status.Active);

        var result = tenant.RetireSigningKey(Guid.NewGuid());

        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }

    [Fact]
    public void Retire_RefusesAnArchivedTenant()
    {
        var (tenant, previous) = TenantBuilder.Create("acme", Status.Active);
        Assert.True(tenant.RotateSigningKey(NewKey()).IsSuccess);
        Assert.True(tenant.Archive().IsSuccess);

        var result = tenant.RetireSigningKey(previous.SigningKeyId);

        Assert.True(result.IsFailed);
        Assert.Equal(SigningKeyStatus.Retiring, previous.Status);
    }
}
