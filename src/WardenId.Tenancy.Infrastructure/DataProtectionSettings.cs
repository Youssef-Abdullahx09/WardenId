namespace WardenId.Tenancy.Infrastructure;

/// <summary>ASP.NET Core Data Protection settings. Changing the application name makes every protected private key unreadable.</summary>
internal static class DataProtectionSettings
{
    public const string ApplicationName = "WardenId";
}
