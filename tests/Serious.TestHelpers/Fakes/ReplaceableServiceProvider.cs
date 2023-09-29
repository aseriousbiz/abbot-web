using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Serious.TestHelpers;

/// <summary>
/// An implementation of <see cref="IServiceProvider"/> that wraps an existing <see cref="IServiceProvider"/>
/// but allows us to replace a service with our own implementation.
/// </summary>
public class ReplaceableServiceProvider : IServiceProvider, IServiceScopeFactory, IServiceScope
{
    readonly IServiceProvider _serviceProvider;
    readonly Dictionary<Type, object> _services = new();

    public ReplaceableServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _services[typeof(IServiceProvider)] = this;
        _services[typeof(IServiceScopeFactory)] = this;
        _services[typeof(IServiceScope)] = this;
    }

    public void ReplaceService<T>(T serviceInstance) where T : notnull
    {
        ReplaceService(typeof(T), serviceInstance);
    }

    public void ReplaceService(Type type, object instance)
    {
        _services[type] = instance;
    }

    public object? GetService(Type serviceType)
    {
        return _services.TryGetValue(serviceType, out var result)
            ? result
            : _serviceProvider.GetService(serviceType);
    }

    public IServiceScope CreateScope()
    {
        return this;
    }

    public void Dispose()
    {
    }

    public IServiceProvider ServiceProvider => this;
}
