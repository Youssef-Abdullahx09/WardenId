using WardenId.Audit.Application;

namespace WardenId.Audit.Infrastructure;

public sealed class AsyncLocalAuditContextAccessor : IAuditContextAccessor
{
    private static readonly AsyncLocal<AuditAmbientContext?> Ambient = new();

    public AuditAmbientContext? Current => Ambient.Value;

    public IDisposable BeginScope(AuditAmbientContext context)
    {
        var previous = Ambient.Value;
        Ambient.Value = context;
        return new Scope(previous);
    }

    private sealed class Scope(AuditAmbientContext? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Ambient.Value = previous;
        }
    }
}
