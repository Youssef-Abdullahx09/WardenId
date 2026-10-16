namespace WardenId.Audit.Application.Tests.Fakes;

public sealed class FakeAuditEventSearch : IAuditEventSearch
{
    public List<AuditEventSearchRow> Rows { get; } = [];
    public AuditEventFilter? LastFilter { get; private set; }

    public Task<(long Total, IReadOnlyList<AuditEventSearchRow> Rows)> SearchAsync(
        AuditEventFilter filter, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        LastFilter = filter;
        IReadOnlyList<AuditEventSearchRow> page = Rows.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray();
        return Task.FromResult(((long)Rows.Count, page));
    }
}
