using System.Data;
using Dapper;

namespace WardenId.SharedKernel.Query.Utilities;

public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly?>
{
    public override DateOnly? Parse(object value)
    {
        if (value == DBNull.Value) return null;

        // Handle both DateTime and DateOnly
        return value switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            DateOnly d => d,
            _ => throw new DataException($"Cannot convert {value.GetType()} to DateOnly")
        };
    }

    public override void SetValue(IDbDataParameter parameter, DateOnly? value)
    {
        if (value.HasValue)
        {
            // Npgsql now supports DateOnly directly
            parameter.Value = value.Value;
            parameter.DbType = DbType.Date;
        }
        else
        {
            parameter.Value = DBNull.Value;
        }
    }
}
