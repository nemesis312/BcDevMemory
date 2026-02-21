using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace DevMemory.Cli.Infrastructure;

/// <summary>
/// Bridges Microsoft.Extensions.DependencyInjection with Spectre.Console.Cli's
/// type registration system so command dependencies resolve via the DI container.
/// </summary>
internal sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;

    public TypeRegistrar(IServiceCollection services) => _services = services;

    public ITypeResolver Build() => new TypeResolver(_services.BuildServiceProvider());

    public void Register(Type service, Type implementation) =>
        _services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) =>
        _services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory) =>
        _services.AddSingleton(service, _ => factory());
}

internal sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider) => _provider = provider;

    public object? Resolve(Type? type) =>
        type == null ? null : _provider.GetService(type);

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
            disposable.Dispose();
    }
}
