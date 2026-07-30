using WardenId.Tenancy.Application.Features.Tenants.Create;

namespace WardenId.Tenancy.Presentation.Features.Tenants.Create;

public sealed record Request(
    string Slug,
    string DisplayName)
{
    public Command ToCommand(
        Guid userId) => new(Slug, DisplayName, userId);
}
