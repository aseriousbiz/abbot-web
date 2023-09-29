using System;
using System.Collections.Generic;

namespace Serious.TestHelpers
{
    /// <summary>
    /// An implementation of <see cref="IServiceProvider"/> that lets us provide a backup
    /// service for an unregistered service.
    /// </summary>
    public class FakeServiceProvider : IServiceProvider
    {
        readonly IServiceProvider? _serviceProvider;
        readonly Dictionary<Type, object> _services = new();

        public FakeServiceProvider() : this(null)
        {
        }

        public FakeServiceProvider(IServiceProvider? serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void AddService<T>(T serviceInstance) where T : notnull
        {
            AddService(typeof(T), serviceInstance);
        }

        public void AddService(Type type, object instance)
        {
            _services.Add(type, instance);
        }

        public object GetService(Type serviceType)
        {
            return _serviceProvider?.GetService(serviceType)
                ?? _services[serviceType];
        }
    }
}
