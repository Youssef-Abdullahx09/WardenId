namespace WardenId.SharedKernel.Presentation;

public interface ICurrentTenant
{
    string Slug { get; }
    Guid TenantId { get; }
}