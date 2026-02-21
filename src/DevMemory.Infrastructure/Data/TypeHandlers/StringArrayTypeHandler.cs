using System.Data;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace DevMemory.Infrastructure.Data.TypeHandlers;

/// <summary>
/// Tells Dapper how to send/receive PostgreSQL TEXT[] arrays.
/// Without this, Dapper passes string[] as a generic object and Npgsql fails.
/// </summary>
public sealed class StringArrayTypeHandler : SqlMapper.TypeHandler<string[]>
{
#pragma warning disable CS8765 // Dapper base declares object (non-nullable); safe to handle null here
    public override string[] Parse(object value) => value switch
#pragma warning restore CS8765
    {
        string[] arr => arr,
        DBNull => Array.Empty<string>(),
        _ when value is null => Array.Empty<string>(),
        _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to string[]")
    };

    public override void SetValue(IDbDataParameter parameter, string[] value)
    {
        var p = (NpgsqlParameter)parameter;
        p.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text;
        p.Value = value ?? Array.Empty<string>();
    }
}
