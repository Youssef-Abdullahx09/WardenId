namespace WardenId.ClientManagement.Domain.Tests;

public class ClientErrorsTests
{
    [Fact]
    public void Validation_SetsValidationErrorCodeMetadata()
    {
        var error = ClientErrors.Validation("some reason");

        Assert.Equal("some reason", error.Message);
        Assert.Equal(ClientErrorCodes.Validation, error.Metadata[ClientErrorCodes.MetadataKey]);
    }

    [Fact]
    public void NotFound_SetsNotFoundErrorCodeMetadata()
    {
        var error = ClientErrors.NotFound;

        Assert.Equal(ClientErrorCodes.NotFound, error.Metadata[ClientErrorCodes.MetadataKey]);
    }
}
