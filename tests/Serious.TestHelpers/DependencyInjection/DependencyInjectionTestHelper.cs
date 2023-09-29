#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

public class ScopedProvider : IServiceProvider, IServiceScopeFactory, IServiceScope
{
    IServiceScope? _scope;
    readonly ServiceCollection _serviceCollection = new ServiceCollection();
    readonly IServiceProvider _provider;

    public ScopedProvider(Func<IServiceCollection, IServiceProvider> providerFunc)
    {
        _provider = providerFunc(_serviceCollection);
        CreateScope();
    }

    public IServiceScope CreateScope()
    {
        Dispose();

        _scope = _provider.CreateScope();
        var exceptions = new List<InvalidOperationException>();

        foreach (var serviceDescriptor in _serviceCollection)
        {
            var serviceType = serviceDescriptor.ServiceType;
            if (serviceType.ContainsGenericParameters)
            {
                continue;
            }

            var serviceTypeNamespace = serviceType.Namespace;
            if (serviceTypeNamespace is null)
            {
                continue;
            }

            if (serviceTypeNamespace.Equals("Serious") || serviceTypeNamespace.StartsWith("Serious."))
            {
                try
                {
                    if (serviceDescriptor.Lifetime != ServiceLifetime.Scoped)
                    {
                        _scope.ServiceProvider.GetService(serviceType);
                    }
                    else
                    {
                        _scope.ServiceProvider.CreateScope().ServiceProvider.GetService(serviceType);
                    }
                }
                catch (Exception e)
                {
                    exceptions.Add(new InvalidOperationException($"Could not create {serviceType}", e));
                }
            }
        }

        if (exceptions.Any())
        {
            throw new AggregateException("Some services are missing", exceptions);
        }

        return this;
    }

    public IServiceProvider ServiceProvider => _scope!.ServiceProvider;

    public object? GetService(Type serviceType) => _scope!.ServiceProvider.GetService(serviceType);

    public void Dispose()
    {
        _scope?.Dispose();
        _scope = null;
    }
}
