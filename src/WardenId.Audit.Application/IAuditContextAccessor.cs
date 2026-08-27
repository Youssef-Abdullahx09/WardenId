namespace WardenId.Audit.Application;

/// <summary>
/// I7 — backed by AsyncLocal, never IHttpContextAccessor. HTTP middleware, CAP consumers,
/// and hosted services all populate this the same way; none of them can assume a
/// HttpContext exists.
/// </summary>
public interface IAuditContextAccessor
{
    AuditAmbientContext? Current { get; }

    /// <summary>Sets Current for the duration of the returned scope, restoring the previous value on Dispose.</summary>
    IDisposable BeginScope(AuditAmbientContext context);
}
