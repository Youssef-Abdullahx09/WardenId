using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;
using WardenId.Protocol.Presentation.Utilities;

namespace WardenId.Protocol.Presentation;

/// <summary>Auth pages are never cached and never framed (clickjacking).</summary>
internal sealed class AuthPageHeadersFilter : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.CacheControl = CacheControlHeaderValue.NoStoreString;
        headers.Pragma = CacheControlHeaderValue.NoCacheString;
        headers.XFrameOptions = HeaderValues.FrameOptionsDeny;
        headers.ContentSecurityPolicy = HeaderValues.FrameAncestorsNone;
        return next();
    }
}
