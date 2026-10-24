using WardenId.Gateway.Utilities;
using WardenId.SharedKernel.Query.Contracts;
using ClientsListItem = WardenId.ClientManagement.Query.Contracts.Features.Clients.List.Item;
using UsersListItem = WardenId.Identity.Query.Contracts.Features.Users.List.Item;

namespace WardenId.Gateway.Tests.Swagger;

public class SchemaIdsTests
{
    [Fact]
    public void For_KeepsSameNamedTypeArgumentsApart()
    {
        Assert.NotEqual(
            SchemaIds.For(typeof(PaginationListResponse<UsersListItem>)),
            SchemaIds.For(typeof(PaginationListResponse<ClientsListItem>)));
    }

    [Fact]
    public void For_QualifiesGenericTypeArguments()
    {
        Assert.Equal(
            "WardenId.SharedKernel.Query.Contracts.PaginationListResponseOfWardenId.Identity.Query.Contracts.Features.Users.List.Item",
            SchemaIds.For(typeof(PaginationListResponse<UsersListItem>)));
    }

    [Fact]
    public void For_UsesTheFullNameOfNonGenericTypes()
    {
        Assert.Equal(
            "WardenId.Identity.Query.Contracts.Features.Users.List.Item",
            SchemaIds.For(typeof(UsersListItem)));
    }
}
