using System;
using Microsoft.Extensions.DependencyInjection;

namespace Serious.Abbot.Services;

public static class ServiceProviderExtensions
{
    /// <inheritdoc cref="ActivatorUtilities.CreateInstance(IServiceProvider, Type, object[])" />
    public static object Activate(this IServiceProvider provider, Type type, params object[] parameters) =>
        ActivatorUtilities.CreateInstance(provider, type, parameters);

    /// <inheritdoc cref="ActivatorUtilities.CreateInstance{T}(IServiceProvider, object[])" />
    public static T Activate<T>(this IServiceProvider provider, params object[] parameters) =>
        ActivatorUtilities.CreateInstance<T>(provider, parameters);
}
