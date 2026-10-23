namespace WardenId.Identity.Domain.Tests;

public class UserSoftDeleteTests
{
    private static User CreateUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    [Fact]
    public void SoftDelete_SetsIsDeletedAndDeletedAt_AndKeepsChildren()
    {
        var user = CreateUser();
        var factor = user.AddEnabledTotp();

        Assert.True(user.SoftDelete().IsSuccess);

        Assert.Equal(UserStatus.SoftDeleted, user.Status);
        Assert.True(user.IsDeleted);
        Assert.NotNull(user.DeletedAt);
        Assert.Contains(user.MfaFactors, f => f.Id == factor.Id);
    }

    [Fact]
    public void Restore_ClearsIsDeletedAndDeletedAt()
    {
        var user = CreateUser();
        Assert.True(user.SoftDelete().IsSuccess);

        Assert.True(user.Restore().IsSuccess);

        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.False(user.IsDeleted);
        Assert.Null(user.DeletedAt);
    }
}
