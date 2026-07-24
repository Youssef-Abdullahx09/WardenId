using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Diagnostics;

namespace WardenId.Gateway.Utilities;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var requestSnapshot = await httpContext.Request.CaptureAsync(
            httpContext.User,
            httpContext,
            cancellationToken);

        var statusCode = exception switch
        {
            ValidationException => HttpStatusCode.BadRequest,
            KeyNotFoundException => HttpStatusCode.NotFound,
            _ => HttpStatusCode.InternalServerError
        };

        logger.LogCritical(
            exception,
            "Unhandled exception {@Request} {@Error}",
            requestSnapshot,
            new
            {
                ExceptionType = exception.GetType().Name,
                exception.Message,
                StatusCode = (int)statusCode
            });

        httpContext.Response.StatusCode = (int)statusCode;
        await httpContext.Response.WriteAsJsonAsync("An unexpected error occurred, please contact support.", cancellationToken);
        
        return true;
    }
}
