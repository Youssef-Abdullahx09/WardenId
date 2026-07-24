namespace WardenId.ClientManagement.Domain;

[Flags]
public enum GrantType
{
    AuthorizationCode = 1,
    ClientCredentials = 2,
    RefreshToken = 4,
    DeviceCode = 8
}