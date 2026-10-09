namespace WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;

public enum AllowedGrant
{
    AuthorizationCode = 1,
    ClientCredentials = 2,
    RefreshToken = 3,
    DeviceCode = 4
}
