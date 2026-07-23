using System.Diagnostics;
using System.Security.Claims;
using System.Text;

namespace WardenId.Gateway.Utilities;

internal static class HttpRequestExtensions
{
    public static async Task<HttpRequestSnapshot> CaptureAsync(
        this HttpRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        CancellationToken ct)
    {
        object? body = null;
        
        // JSON body
        if (request.ContentType?.Contains("application/json") == true)
        {
            request.EnableBuffering();

            using var reader = new StreamReader(
                request.Body, Encoding.UTF8, leaveOpen: true);

            body = await reader.ReadToEndAsync(ct);
            request.Body.Position = 0;
        }
        // FORM body (without files)
        else if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(ct);

            body = form
                .Where(f => f.Value.Count > 0) // excludes files automatically
                .ToDictionary(
                    f => f.Key,
                    f => f.Value.ToString()
                );
        }

        return new HttpRequestSnapshot(
            Method: request.Method,
            Path: request.Path,
            QueryString: request.QueryString.Value,
            Headers: request.Headers
                .Where(h => !IsSensitiveHeader(h.Key))
                .ToDictionary(h => h.Key, h => h.Value.ToString()),
            Body: body,
            UserId: user.FindFirstValue(ClaimTypes.NameIdentifier),
            TraceId: Activity.Current?.Id ?? context.TraceIdentifier,
            IpAddress: context.Connection.RemoteIpAddress?.ToString(),
            UserAgent: request.Headers.UserAgent.ToString()
        );
    }

    private static bool IsSensitiveHeader(string header) =>
        header.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
        header.Equals("Cookie", StringComparison.OrdinalIgnoreCase);
}
