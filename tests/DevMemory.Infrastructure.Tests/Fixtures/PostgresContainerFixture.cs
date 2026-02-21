using DevMemory.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace DevMemory.Infrastructure.Tests.Fixtures;

/// <summary>
/// Spins up a real PostgreSQL container once per test collection,
/// runs all migrations, and exposes a DapperContext to each test.
/// Shared across tests in the same collection — container is NOT
/// restarted between tests; use transactions or clean up in tests.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("devmemory_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public DapperContext Context { get; private set; } = null!;
    public DatabaseMigrator Migrator { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Context  = new DapperContext(_container.GetConnectionString());
        Migrator = new DatabaseMigrator(Context);
        await Migrator.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        Context.Dispose();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "Postgres";
}
