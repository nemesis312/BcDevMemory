using Dapper;
using DevMemory.Infrastructure.Data.TypeHandlers;
using Npgsql;

namespace DevMemory.Infrastructure.Data;

/// <summary>
/// Wraps NpgsqlDataSource for connection pooling and registers Dapper type handlers.
/// Inject as a singleton — NpgsqlDataSource is thread-safe and manages the pool.
/// </summary>
public sealed class DapperContext : IDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    static DapperContext()
    {
        // Register once per process — these handle Npgsql 8+ timestamp changes
        // and PostgreSQL TEXT[] arrays for all Dapper queries.
        SqlMapper.AddTypeHandler(new DateTimeTypeHandler());
        SqlMapper.AddTypeHandler(new NullableDateTimeTypeHandler());
        SqlMapper.AddTypeHandler(new StringArrayTypeHandler());
    }

    public DapperContext(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string must not be empty.", nameof(connectionString));

        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    /// <summary>
    /// Opens and returns a new database connection from the pool.
    /// Caller is responsible for disposal (use 'await using' or 'using').
    /// </summary>
    public NpgsqlConnection CreateConnection() => _dataSource.CreateConnection();

    public void Dispose() => _dataSource.Dispose();
}
