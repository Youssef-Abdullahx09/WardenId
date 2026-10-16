using WardenId.Audit.Application;

namespace WardenId.Audit.Application.Tests.Fakes;

public sealed class FakeAuditContextAccessor : IAuditContextAccessor
{
    public AuditAmbientContext? Current { get; set; }

    public IDisposable BeginScope(AuditAmbientContext context)
    {
        var previous = Current;
        Current = context;
        return new Restore(() => Current = previous);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
