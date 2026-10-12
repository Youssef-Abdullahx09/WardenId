using Microsoft.AspNetCore.Mvc.Filters;

namespace WardenId.Protocol.Presentation;

/// <summary>Auth pages are never cached and never framed (clickjacking).</summary>
internal sealed class AuthPageHeadersFilter : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.CacheControl = "no-store";
        headers.Pragma = "no-cache";
        headers.XFrameOptions = "DENY";
        headers.ContentSecurityPolicy = "frame-ancestors 'none'";
        return next();
    }
}
