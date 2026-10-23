namespace WardenId.Tenancy.Query.Contracts.Features.Tenants.GetBySlug;

/// <summary>
/// A tenant by its (already validated) slug, compared exactly; answered with
/// <see cref="Get.Item"/>.
/// </summary>
public sealed record Query(string Slug);
