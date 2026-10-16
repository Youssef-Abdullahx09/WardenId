using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Features.Tenants.List;

public sealed record TenantListItem(Guid Id, string Slug, string DisplayName, Status Status, DateTimeOffset CreatedAt);
