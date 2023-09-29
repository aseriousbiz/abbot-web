using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Serious.TestHelpers
{
    public static class ServiceCollectionExtensions
    {
        public static void ReplaceIt<TInterface>(
            this IServiceCollection serviceCollection,
            TInterface substitute,
            ServiceLifetime serviceLifetime = ServiceLifetime.Transient) where TInterface : notnull
        {
            serviceCollection.Replace(
                new ServiceDescriptor(
                    typeof(TInterface),
                    _ => substitute,
                    serviceLifetime));
        }

        public static void ReplaceIt<TInterface>(
            this IServiceCollection serviceCollection,
            Func<IServiceProvider, TInterface> substituteFunc,
            ServiceLifetime serviceLifetime = ServiceLifetime.Transient) where TInterface : notnull
        {
            serviceCollection.Replace(
                new ServiceDescriptor(
                    typeof(TInterface),
                    sp => substituteFunc(sp),
                    serviceLifetime));
        }

        public static void ReplaceIt(
            this IServiceCollection serviceCollection,
            Type sourceType,
            Type replacementType,
            ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        {
            serviceCollection.Replace(
                new ServiceDescriptor(
                    sourceType,
                    replacementType,
                    serviceLifetime));
        }
    }
}
