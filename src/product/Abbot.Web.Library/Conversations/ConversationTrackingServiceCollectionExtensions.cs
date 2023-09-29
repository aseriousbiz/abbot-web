using Serious.Abbot.AI;
using Serious.Abbot.Conversations;

namespace Microsoft.Extensions.DependencyInjection;

public static class ConversationTrackingServiceCollectionExtensions
{
    public static void AddConversationTracking(this IServiceCollection services, bool includeListeners = true)
    {
        services.AddScoped<ConversationMatcher>();
        services.AddScoped<ISanitizedConversationHistoryBuilder, SanitizedConversationHistoryBuilder>();
        services.AddTransient<IConversationPublisher, ConversationPublisher>(); // TODO: This shouldn't need to be transient
        services.AddScoped<IConversationTracker, ConversationTracker>();
        services.AddScoped<IConversationThreadResolver, ConversationThreadResolver>();

        // Tests provide fake listeners, or register listeners manually when they're needed.
        if (includeListeners)
        {
            services.AddScoped<IConversationListener, ConversationWelcomeListener>();
            services.AddScoped<IConversationListener, FirstResponderNotificationListener>();
        }

        services.AddScoped<IMissingConversationsReporter, MissingConversationsReporter>();
        services.AddScoped<HubMessageRenderer>();
    }
}
