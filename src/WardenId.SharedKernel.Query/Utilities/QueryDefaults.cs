namespace WardenId.SharedKernel.Query.Utilities;

public static class QueryDefaults
{
    /// <summary>Column Dapper splits multi-mapped rows on by default.</summary>
    public const string SplitOn = "Id";
}
