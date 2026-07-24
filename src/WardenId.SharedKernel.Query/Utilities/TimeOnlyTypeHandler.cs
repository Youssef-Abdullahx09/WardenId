using System.Data;
using Dapper;

namespace WardenId.SharedKernel.Query.Utilities;

public class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly?>
{
    public override TimeOnly? Parse(object value)
    {
        if (value == DBNull.Value) return null;

        return value switch
        {
            TimeSpan ts => TimeOnly.FromTimeSpan(ts),   // typical PostgreSQL TIME
            TimeOnly t => t,                            // already TimeOnly (Npgsql >=7)
            _ => throw new DataException($"Cannot convert {value.GetType()} to TimeOnly")
        };
    }

    public override void SetValue(IDbDataParameter parameter, TimeOnly? value)
    {
        if (value.HasValue)
        {
            parameter.Value = value.Value;
            parameter.DbType = DbType.Time;
        }
        else
        {
            parameter.Value = DBNull.Value;
        }
    }
}
