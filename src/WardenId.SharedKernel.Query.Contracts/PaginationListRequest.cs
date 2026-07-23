namespace WardenId.SharedKernel.Query.Contracts;

public abstract record PaginationListRequest(
    int PageNumber,
    int PageSize,
    SortDirection SortDirection = SortDirection.Asc);