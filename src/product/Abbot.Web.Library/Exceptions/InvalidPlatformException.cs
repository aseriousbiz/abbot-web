using System;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;

namespace Serious.Abbot.Exceptions;

/// <summary>
/// Exception thrown when we receive a message in an unexpected platform.
/// </summary>
public class InvalidPlatformException : Exception
{
    public InvalidPlatformException()
    {
    }

    public InvalidPlatformException(string message) : base(message)
    {
    }

    public InvalidPlatformException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public InvalidPlatformException(string platformTypeText, Activity activity)
        : this(GetMessageFromActivity(platformTypeText, activity))
    {
        From = activity.From?.Id ?? "(null)";
        Recipient = activity.Recipient?.Id ?? "(null)";
    }

    public string From { get; } = string.Empty;

    public string Recipient { get; } = string.Empty;

    static string GetMessageFromActivity(string platformTypeText, IMessageActivity activity)
    {
        var channelData = activity.ChannelData is null
            ? "(null)"
            : JsonConvert.SerializeObject(activity.ChannelData, Formatting.Indented);

        return @$"Could not parse platform type '{platformTypeText}'. 
Type: '{activity.Type}' 
Channel: '{activity.ChannelId}' 
Conversation: '{activity.Conversation?.Id}' 
TextFormat: '{activity.TextFormat}' 
Text: '{activity.Text}' 
From.Id: '{activity.From?.Id}' 
Recipient.Id: '{activity.Recipient?.Id}' 
ChannelData: '{channelData}'";
    }
}
