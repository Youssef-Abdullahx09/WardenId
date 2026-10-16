using FluentResults;
using MediatR;
using WardenId.SharedKernel.Query.Contracts;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Features.Tenants.List;

public sealed record Query(int PageNumber, int PageSize, Status? Status)
    : IRequest<Result<PaginationListResponse<TenantListItem>>>;
