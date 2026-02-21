using Dapper;
using DevMemory.Infrastructure.Data.TypeHandlers;
using Microsoft.Data.Sqlite;
using System.Reflection;

namespace DevMemory.Infrastructure.Data;

/// <summary>
/// Creates SQLite connections and runs the schema migration on first use.
/// Inject as a singleton — connection is per-operation (SQLite WAL mode handles concurrency).
/// </summary>
public sealed class SqliteContext
{
    private readonly string _connectionString;
    private bool _migrated;
    private readonly SemaphoreSlim _migrateLock = new(1, 1);

    static SqliteContext()
    {
        SqlMapper.AddTypeHandler(new DateTimeTypeHandler());
        SqlMapper.AddTypeHandler(new NullableDateTimeTypeHandler());
    }

    public SqliteContext(string databasePath)
    {
        var expanded = databasePath.Replace("~",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        var dir = Path.GetDirectoryName(expanded);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = $"Data Source={expanded};Cache=Shared";
    }

    public SqliteConnection CreateConnection() => new(_connectionString);

    /// <summary>
    /// Runs 005_SqliteSchema.sql idempotently. Call once at startup.
    /// </summary>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_migrated) return;

        await _migrateLock.WaitAsync(cancellationToken);
        try
        {
            if (_migrated) return;

            var sql = LoadScript("005_SqliteSchema.sql");
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(sql);
            _migrated = true;
        }
        finally
        {
            _migrateLock.Release();
        }
    }

    private static string LoadScript(string fileName)
    {
        var assembly = typeof(SqliteContext).Assembly;
        var resourceName = $"DevMemory.Infrastructure.Data.Scripts.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Embedded SQL script not found: {resourceName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
