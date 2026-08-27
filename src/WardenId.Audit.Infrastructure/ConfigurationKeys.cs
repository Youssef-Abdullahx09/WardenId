namespace WardenId.Audit.Infrastructure;

public static class ConfigurationKeys
{
    public static class Sealer
    {
        public const string Section = "Audit:Sealer";
        public const string PollIntervalSeconds = nameof(PollIntervalSeconds);
        public const string MaxSegmentSize = nameof(MaxSegmentSize);
        public const string MaxSegmentAgeSeconds = nameof(MaxSegmentAgeSeconds);
        public const string MaxSegmentsPerCheckpoint = nameof(MaxSegmentsPerCheckpoint);
        public const string MaxCheckpointIntervalSeconds = nameof(MaxCheckpointIntervalSeconds);
    }

    public static class Retention
    {
        public const string Section = "Audit:Retention";
        public const string PollIntervalSeconds = nameof(PollIntervalSeconds);
        public const string AuthnInteractiveDays = nameof(AuthnInteractiveDays);
        public const string AuthnSilentDays = nameof(AuthnSilentDays);
        public const string AuthnMachineDays = nameof(AuthnMachineDays);
        public const string AdminDirectoryDays = nameof(AdminDirectoryDays);
        public const string AdminConfigDays = nameof(AdminConfigDays);
        public const string TokenOpsDays = nameof(TokenOpsDays);
        public const string PlatformDays = nameof(PlatformDays);

        // Not bound from config — a distinct, narrowly-scoped DB role for the retention purge
        // path (spec §8) requires manual provisioning; see docs/audit/db-roles.sql. Falls back
        // to the default connection string, with a startup warning, when unset.
        public const string RetentionConnectionStringKey = "AuditRetention";
    }

    public static class Exporter
    {
        public const string Section = "Audit:Exporter";
        public const string PollIntervalSeconds = nameof(PollIntervalSeconds);
    }

    public static class Witness
    {
        public const string Section = "Audit:Witness";
        public const string AppendOnlyFilePath = nameof(AppendOnlyFilePath);
        public const string WebhookUrl = nameof(WebhookUrl);
    }
}
