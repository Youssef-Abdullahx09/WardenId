using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.Protocol.Presentation.Tests.Fakes;

/// <summary>
/// Stands in for the Audit module's actor scope (AsyncLocal, like the real one). The fake Identity handlers call
/// <see cref="Observe"/>, so a test sees whom Identity's writes would have been attributed to.
/// </summary>
public sealed class FakeAuditActorScope : IAuditActorScope
{
    private static readonly AsyncLocal<(Guid TenantId, Guid UserId)?> Acting = new();
    private readonly Lock _gate = new();
    private readonly List<(string Request, (Guid TenantId, Guid UserId)? ActingAs)> _observed = [];

    public IReadOnlyList<(string Request, (Guid TenantId, Guid UserId)? ActingAs)> Observed
    {
        get
        {
            lock (_gate) return _observed.ToArray();
        }
    }

    public IDisposable BeginEndUser(Guid tenantId, Guid userId)
    {
        var previous = Acting.Value;
        Acting.Value = (tenantId, userId);
        return new Scope(previous);
    }

    public void Observe(string request)
    {
        lock (_gate) _observed.Add((request, Acting.Value));
    }

    private sealed class Scope((Guid, Guid)? previous) : IDisposable
    {
        public void Dispose() => Acting.Value = previous;
    }
}
