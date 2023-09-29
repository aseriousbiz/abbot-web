using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Serious;

[SuppressMessage("Microsoft.Performance", "CA1812", Justification = "Class instantiated via dependency injection")]
internal class LazyDependency<T> : Lazy<T> where T : class
{
    public LazyDependency(IServiceProvider provider)
        : base(provider.GetRequiredService<T>)
    {
    }
}

public static class LazyDependencyExtensions
{
    public static void AddLazyDependencySupport(this IServiceCollection services)
    {
        services.AddTransient(typeof(Lazy<>), typeof(LazyDependency<>));
    }
}
