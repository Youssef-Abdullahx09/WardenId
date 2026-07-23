using FluentResults;
using Microsoft.AspNetCore.Http;

namespace WardenId.SharedKernel.Presentation;

public static class IResultExtensions
{
    public static IResult HandleApiFailure<TValue>(
        this Result<TValue> result)
    {
        return result switch
        {
            { IsSuccess: true } => throw new InvalidOperationException(),
            _ => result.Errors.HandleApiFailure()
        };
    }
    
    public static IResult HandleApiFailure(
        this Result result)
    {
        return result switch
        {
            { IsSuccess: true } => throw new InvalidOperationException(),
            _ => result.Errors.HandleApiFailure()
        };
    }
    
    private static IResult HandleApiFailure(this IEnumerable<IError> errors)
    {
        var errorList = errors.ToList();
        var first = errorList.FirstOrDefault();
        var message = first?.Message ?? "Request failed.";

        var statusCode = first?.Metadata.GetValueOrDefault(ErrorCodes.MetadataKey) switch
        {
            ErrorCodes.InvalidCredentials => StatusCodes.Status401Unauthorized,
            ErrorCodes.InvalidToken => StatusCodes.Status401Unauthorized,
            ErrorCodes.AccountDeactivated => StatusCodes.Status403Forbidden,
            ErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
            ErrorCodes.NotFound => StatusCodes.Status404NotFound,
            ErrorCodes.DuplicateIdentity => StatusCodes.Status409Conflict,
            ErrorCodes.AccountLocked => StatusCodes.Status423Locked,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(detail: message, statusCode: statusCode);
    }
}