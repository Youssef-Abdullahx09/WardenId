namespace WardenId.SharedKernel.Infrastructure;

public static class ConfigurationKeys
{
    public const string DbConnectionStringKey = "DefaultConnectionString";
    public const string BlobStorageConnectionStringKey = "BlobStorage:ConnectionString";

    public static class CapPublisher
    {
        public const string Section = nameof(CapPublisher);
        public const string FallbackWindowLookBackSeconds = nameof(FallbackWindowLookBackSeconds);
        public const string RetryCount = nameof(RetryCount);
        public const string RetryInterval = nameof(RetryInterval);
        public const string SucceedMessageExpiredAfter = nameof(SucceedMessageExpiredAfter);
        public const string FailedMessageExpiredAfter = nameof(FailedMessageExpiredAfter);
        
    }
    
    public static class RabbitMq
    {
        public const string Section = "RabbitMq";
        public const string Host = nameof(Host);
        public const string Port = nameof(Port);
        public const string Username = nameof(Username);
        public const string Password = nameof(Password);
        public const string PrefetchCount = nameof(PrefetchCount);
    }

    public static class Smtp
    {
        public const string Section = nameof(Smtp);
        public const string Host = nameof(Host);
        public const string Port = nameof(Port);
        public const string Username = nameof(Username);
        public const string Password = nameof(Password);
        public const string EnableSsl = nameof(EnableSsl);
        public const string FromAddress = nameof(FromAddress);
        public const string FromDisplayName = nameof(FromDisplayName);
    }
}