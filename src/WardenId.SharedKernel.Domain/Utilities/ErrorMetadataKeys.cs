namespace WardenId.SharedKernel.Domain.Utilities;

/// <summary>Metadata keys attached to <c>FluentResults.Error</c>s; the HTTP boundary reads them to pick a status code.</summary>
public static class ErrorMetadataKeys
{
    public const string ErrorCode = "ErrorCode";
}
