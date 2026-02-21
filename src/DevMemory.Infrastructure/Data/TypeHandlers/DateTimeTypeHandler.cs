using System.Data;
using Dapper;

namespace DevMemory.Infrastructure.Data.TypeHandlers;

/// <summary>
/// Npgsql 8+ maps timestamptz → DateTimeOffset by default.
/// These handlers let Dapper map to/from DateTime (UTC) transparently.
/// </summary>
public sealed class DateTimeTypeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override DateTime Parse(object value) => value switch
    {
        DateTimeOffset dto => dto.UtcDateTime,
        DateTime dt => dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime(),
        _ => Convert.ToDateTime(value).ToUniversalTime()
    };

    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.Value = new DateTimeOffset(
            value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime());
    }
}

public sealed class NullableDateTimeTypeHandler : SqlMapper.TypeHandler<DateTime?>
{
    public override DateTime? Parse(object value)
    {
        if (value is null or DBNull) return null;
        return value switch
        {
            DateTimeOffset dto => dto.UtcDateTime,
            DateTime dt => dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime(),
            _ => Convert.ToDateTime(value).ToUniversalTime()
        };
    }

    public override void SetValue(IDbDataParameter parameter, DateTime? value)
    {
        if (value is null)
            parameter.Value = DBNull.Value;
        else
            parameter.Value = new DateTimeOffset(
                value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime());
    }
}
