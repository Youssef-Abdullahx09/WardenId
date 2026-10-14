using FluentResults;

namespace WardenId.Protocol.Domain.Tests;

public class SessionErrorsTests
{
    private static object Code(IError error) => error.Metadata[ProtocolErrorCodes.MetadataKey];

    [Fact]
    public void NotFound_IsTaggedNotFound()
    {
        Assert.Equal(ProtocolErrorCodes.NotFound, Code(SessionErrors.NotFound()));
        Assert.Equal("Session not found.", SessionErrors.NotFound().Message);
    }

    [Fact]
    public void AlreadyEnded_IsTaggedValidation() =>
        Assert.Equal(ProtocolErrorCodes.Validation, Code(SessionErrors.AlreadyEnded()));

    [Fact]
    public void Ended_IsTaggedSessionExpired()
    {
        Assert.Equal(ProtocolErrorCodes.SessionExpired, Code(SessionErrors.Ended()));
        Assert.Equal("The session has ended.", SessionErrors.Ended().Message);
    }

    [Fact]
    public void InvalidLifetime_IsTaggedValidation() =>
        Assert.Equal(ProtocolErrorCodes.Validation, Code(SessionErrors.InvalidLifetime()));
}
