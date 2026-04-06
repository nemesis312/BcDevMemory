using System.Reflection;
using Dapper;

namespace DevMemory.Infrastructure.Data;

/// <summary>
/// Runs SQL migration scripts embedded in the assembly, in order.
/// All scripts are idempotent (IF NOT EXISTS / CREATE OR REPLACE).
/// Call once at application startup before accepting requests.
/// </summary>
public sealed class DatabaseMigrator
{
    private readonly DapperContext _context;

    private static readonly string[] ScriptOrder =
    [
        "001_InitialSchema.sql",
        "002_FullTextSearch.sql",
        "003_StoredProcedures.sql",
        "004_EnhancedSearch.sql",
        "006_ArtifactRunState.sql",
    ];

    public DatabaseMigrator(DapperContext context)
    {
        _context = context;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        foreach (var scriptName in ScriptOrder)
        {
            var sql = LoadScript(scriptName);
            await connection.ExecuteAsync(sql);
        }
    }

    private static string LoadScript(string fileName)
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        // Embedded resource names use dots: DevMemory.Infrastructure.Data.Scripts.001_InitialSchema.sql
        var resourceName = $"DevMemory.Infrastructure.Data.Scripts.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Embedded SQL script not found: {resourceName}. " +
                $"Available: [{string.Join(", ", assembly.GetManifestResourceNames())}]");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
