namespace WardenId.Protocol.Infrastructure;

public static class ConfigurationKeys
{
    public static class ProtocolOpenIddict
    {
        /// <summary>Development/tests only: allow OpenIddict endpoints over plain HTTP.</summary>
        public const string DisableTransportSecurityRequirement = "ProtocolOpenIddict:DisableTransportSecurityRequirement";
    }
}
