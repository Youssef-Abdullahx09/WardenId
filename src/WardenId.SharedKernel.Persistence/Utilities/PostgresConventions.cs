namespace WardenId.SharedKernel.Persistence.Utilities;

/// <summary>PostgreSQL / EF Core names shared by every module's DbContext and configurations.</summary>
public static class PostgresConventions
{
    public static class ColumnTypes
    {
        public const string Jsonb = "jsonb";
        public const string Xid = "xid";
    }

    public static class SystemColumns
    {
        /// <summary>Row version system column Npgsql maps a <c>uint</c> concurrency token to.</summary>
        public const string Xmin = "xmin";
    }

    public static class IndexMethods
    {
        public const string Gin = "gin";
    }
}
