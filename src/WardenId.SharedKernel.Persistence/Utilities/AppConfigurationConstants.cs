namespace WardenId.SharedKernel.Persistence.Utilities;

public static class AppConfigurationConstants
{
    public const string DbConnectionStringKey = "DefaultConnectionString";
    public const string AppSettingFileName = "appsettings.json";
    public const string PathToGateway = ".";

    public const string NotFoundConnectionStringMessage =
        $"Connection string '{DbConnectionStringKey}' is missing or empty.";
}
