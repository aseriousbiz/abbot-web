using System;
using Microsoft.Extensions.DependencyInjection;
using Refit;

[assembly: CLSCompliant(false)]
namespace Serious.Slack;

public static class StartupExtensions
{
    /// <summary>
    /// Registers implementations for key Slack services to the container. For example,
    /// <see cref="ISlackApiClient" /> and <see cref="IReactionsApiClient"/> among others.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> for the app.</param>
    public static void AddSlackApiClient(this IServiceCollection services)
    {
        // This is a default inefficient implementation. Clients should replace with a better one.
        services.AddSlackApiClient<CustomEmojiLookup>();
    }

    /// <summary>
    /// Registers implementations for key Slack services to the container. For example,
    /// <see cref="ISlackApiClient" /> and <see cref="IReactionsApiClient"/> among others.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> for the app.</param>
    public static void AddSlackApiClient<TCustomEmojiLookup>(this IServiceCollection services)
        where TCustomEmojiLookup : class, ICustomEmojiLookup
    {
        services.AddTransient<LoggingHttpMessageHandler>();

        services.AddClient<ISlackApiClient>();
        services.AddClient<IConversationsApiClient>();
        services.AddClient<IFilesApiClient>();
        services.AddClient<IReactionsApiClient>();
        services.AddClient<IEmojiClient>();
        services.AddSingleton<IEmojiLookup, EmojiLookup>();
        services.AddSingleton<ICustomEmojiLookup, TCustomEmojiLookup>();
    }

    static void AddClient<T>(this IServiceCollection services) where T : class
    {
        services.AddRefitClient<T>(SlackSerializer.RefitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = ISlackApiClient.ApiUrl)
#if DEBUG
            .AddHttpMessageHandler<LoggingHttpMessageHandler>()
#endif
            ;
    }
}
