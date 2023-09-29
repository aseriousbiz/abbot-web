using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;
using Refit;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Integrations;

// Marker interface for logging
#pragma warning disable CA1040
public interface ITicketLinker { }
#pragma warning restore CA1040

// We only actually depend on ITicketLinker<TSettings>,
// but consistently implementing this will keep some consistency
public interface ITicketLinker<TSettings, TTicket> : ITicketLinker<TSettings>
    where TSettings : class, ITicketingSettings
    where TTicket : notnull
{
    Task<TTicket?> CreateTicketAsync(
        Integration integration,
        TSettings settings,
        IReadOnlyDictionary<string, object?> properties,
        Conversation conversation,
        Member actor);

    Task<ConversationLink?> CreateConversationLinkAsync(
        Integration integration,
        TSettings settings,
        TTicket ticket,
        Conversation conversation,
        Member actor);

#pragma warning disable CA1033
    async Task<ConversationLink?> ITicketLinker<TSettings>.CreateTicketLinkAsync(
#pragma warning restore CA1033
        Integration integration,
        TSettings settings,
        IReadOnlyDictionary<string, object?> properties,
        Conversation conversation,
        Member actor)
    {
        var ticket = await CreateTicketAsync(integration, settings, properties, conversation, actor);
        return ticket is not null
            ? await CreateConversationLinkAsync(
                integration,
                settings,
                ticket,
                conversation,
                actor)
            : null;
    }
}

public interface ITicketLinker<TSettings> : ITicketLinker
    where TSettings : class, ITicketingSettings
{
    Task<ConversationLink?> CreateTicketLinkAsync(
        Integration integration,
        TSettings settings,
        IReadOnlyDictionary<string, object?> properties,
        Conversation conversation,
        Member actor);

    TicketError ParseException(Exception ex) =>
        ex switch
        {
            ApiException apiException =>
                apiException.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => new(TicketErrorReason.Unauthorized),
                    _ => new(
                        TicketErrorReason.ApiError,
                        GetUserInfo(apiException),
                        apiException.Content),
                },
            _ => new(TicketErrorReason.Unknown),
        };

    string? GetUserInfo(ApiException apiException) => null;

    string? GetPendingTicketUrl(Conversation conversation, Member actor) => null;
}

public enum TicketErrorReason
{
    [EnumMember(Value = "unknown")]
    Unknown,

    [EnumMember(Value = "unauthorized")]
    Unauthorized,

    [EnumMember(Value = "api-error")]
    ApiError,

    [EnumMember(Value = "configuration")]
    Configuration,

    [EnumMember(Value = "user-configuration")]
    UserConfiguration,
}

public record TicketError(
    TicketErrorReason Reason,
    string? UserErrorInfo = null,
    string? ExtraInfo = null);

#pragma warning disable CA1032
public class TicketConfigurationException : InvalidOperationException
#pragma warning restore CA1032
{
    public TicketConfigurationException(
        string message,
        TicketErrorReason reason = TicketErrorReason.Configuration)
        : base(message)
    {
        Reason = reason;
    }

    public TicketErrorReason Reason { get; }
}
