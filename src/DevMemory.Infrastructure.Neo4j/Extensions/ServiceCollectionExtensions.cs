using DevMemory.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DevMemory.Infrastructure.Neo4j.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Neo4j context and all repository/service implementations.
    /// Initializes constraints and indexes synchronously before the app starts.
    /// </summary>
    public static IServiceCollection AddNeo4jStorage(
        this IServiceCollection services,
        string uri,
        string user,
        string password,
        string database = "neo4j")
    {
        var context = new Neo4jContext(uri, user, password, database);
        context.EnsureConstraintsAsync().GetAwaiter().GetResult();

        var repository = new Neo4jMemoryRepository(context);

        services.AddSingleton(context);
        services.AddSingleton<IMemoryRepository>(repository);
        services.AddSingleton<IGraphRepository>(repository);
        services.AddSingleton<ISessionRepository, Neo4jSessionRepository>();
        services.AddSingleton<ISearchService,     Neo4jSearchService>();

        return services;
    }
}
