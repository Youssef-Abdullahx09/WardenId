using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.Protocol.Application.Tests.Fakes;

/// <summary>Tracks the acting user like the Audit module's scope does (AsyncLocal), and every scope begun.</summary>
public sealed class FakeAuditActorScope : IAuditActorScope
{
    private static readonly AsyncLocal<(Guid TenantId, Guid UserId)?> Acting = new();

    public (Guid TenantId, Guid UserId)? Current => Acting.Value;

    public List<(Guid TenantId, Guid UserId)> Begun { get; } = [];

    public IDisposable BeginEndUser(Guid tenantId, Guid userId)
    {
        Begun.Add((tenantId, userId));
        var previous = Acting.Value;
        Acting.Value = (tenantId, userId);
        return new Scope(previous);
    }

    private sealed class Scope((Guid, Guid)? previous) : IDisposable
    {
        public void Dispose() => Acting.Value = previous;
    }
}
