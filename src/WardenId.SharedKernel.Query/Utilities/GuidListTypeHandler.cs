using System.Data;
using Dapper;

namespace WardenId.SharedKernel.Query.Utilities;

public sealed class GuidListTypeHandler : SqlMapper.TypeHandler<List<Guid>>
{
    public override void SetValue(IDbDataParameter parameter, List<Guid> value)
        => parameter.Value = value?.ToArray();

    public override List<Guid> Parse(object value)
        => value switch
        {
            Guid[] arr => arr.ToList(),
            _ => throw new DataException($"Cannot convert {value.GetType()}")
        };
}