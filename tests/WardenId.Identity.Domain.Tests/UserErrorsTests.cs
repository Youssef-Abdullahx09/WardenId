namespace WardenId.Identity.Domain.Tests;

public class UserErrorsTests
{
    [Fact]
    public void Validation_SetsValidationErrorCodeMetadata()
    {
        var error = UserErrors.Validation("some reason");

        Assert.Equal("some reason", error.Message);
        Assert.Equal(UserErrorCodes.Validation, error.Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public void NotFound_SetsNotFoundErrorCodeMetadata()
    {
        var error = UserErrors.NotFound;

        Assert.Equal(UserErrorCodes.NotFound, error.Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public void AccountLocked_SetsAccountLockedErrorCodeMetadata()
    {
        var error = UserErrors.AccountLocked(DateTimeOffset.UtcNow.AddMinutes(15));

        Assert.Equal(UserErrorCodes.AccountLocked, error.Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal("AccountLocked", UserErrorCodes.AccountLocked);
    }
}
