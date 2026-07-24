namespace WardenId.SharedKernel.Query.Contracts;

public sealed record PaginationListResponse<T>(
    long RecordsTotal,
    IEnumerable<T> Data);