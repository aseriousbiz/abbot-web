using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Humanizer;
using Serious.Abbot.AI;
using Serious.Abbot.Conversations;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.GitHub;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.MergeDev;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Models;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Outputs;
using Serious.Slack.InteractiveMessages;

namespace Serious.Abbot.Entities;

public class OutputsBuilder
{
    public IDictionary<string, object?> Outputs { get; }

    public OutputsBuilder()
    {
        Outputs = new Dictionary<string, object?>();
    }

    public OutputsBuilder(IEnumerable<KeyValuePair<string, object?>> initial)
    {
        Outputs = new Dictionary<string, object?>(initial);
    }

    public OutputsBuilder SetCustomer(Customer? customer)
    {
        var nowUtc = DateTime.UtcNow; // UGH. Yes, this pains me. I'll pay penance if we survive. -@haacked
        if (customer is not null)
        {
            Outputs["customer"] = CustomerOutput.FromCustomer(customer, nowUtc);

            // Order by shared first, then by oldest first. We then take the first channel.
            if (customer.GetPrimaryRoom() is { } primaryRoom)
            {
                SetChannel(primaryRoom);
            }

            Outputs["channels"] = customer.Rooms.Select(ChannelOutput.FromRoom).ToArray();
        }

        return this;
    }

    public OutputsBuilder SetRoom(Room? room)
    {
        if (room is not null)
        {
            SetCustomer(room.Customer);
            SetChannel(room);
        }

        return this;
    }

    void SetChannel(Room room)
    {
        Outputs["channel"] = ChannelOutput.FromRoom(room);
    }

    /// <summary>
    /// Adds conversation info to the outputs.
    /// </summary>
    /// <param name="conversation">The <see cref="Conversation"/>.</param>
    /// <param name="categories">
    /// For new conversations, we might not have assigned the AI tags to the <see cref="Conversation"/> when this is
    /// called, but we may have the categories already which we can use.</param>
    public OutputsBuilder SetConversation(
        Conversation? conversation,
        IEnumerable<Category>? categories = null)
    {
        if (conversation is not null)
        {
            SetRoom(conversation.Room);

            var output = ConversationOutput.FromConversation(conversation, categories);
            Outputs["conversation"] = output;

            if (!Outputs.ContainsKey("message"))
            {
                Outputs["message"] = output.Message;
            }
        }

        return this;
    }

    public OutputsBuilder SetTicketLink(IntegrationLink? ticketLink)
    {
        if (ticketLink is not null)
        {
            Outputs["ticket"] = new TicketOutput
            {
                Type = ticketLink switch
                {
                    MergeDevTicketLink md => md.IntegrationName,
                    _ => ticketLink.IntegrationType.Humanize(),
                },
                Url = ticketLink.WebUrl?.ToString(),
                ApiUrl = ticketLink.ApiUrl.ToString(),
                TicketId = ticketLink switch
                {
                    ZendeskTicketLink zd => $"{zd.TicketId}",
                    HubSpotTicketLink hs => hs.TicketId,
                    GitHubIssueLink gh => $"{gh.Number}",
                    _ => null,
                },
                GitHub = ticketLink is GitHubIssueLink ghLink
                    ? new(ghLink.Owner, ghLink.Repo)
                    : null,
                HubSpot = ticketLink is HubSpotTicketLink hsLink
                    ? new(hsLink.ThreadId?.ToStringInvariant())
                    : null,
                Zendesk = ticketLink is ZendeskTicketLink zdLink
                    ? new()
                    {
                        Status = zdLink.Status ?? "Unknown",
                    }
                    : null,
            };
        }

        return this;
    }

    public OutputsBuilder SetMessage(Room room, ConversationMessage message) =>
        SetMessage(room, message.MessageId, message.ThreadId, message.Text, message.GetMessageUrl());

    public OutputsBuilder SetMessage(Room room, SlackMessage message, Uri messageUrl)
    {
        return SetMessage(room, message.Timestamp, message.ThreadTimestamp, message.Text, messageUrl);
    }

    public OutputsBuilder SetMessage(Room room, MessageInfo message)
    {
        return SetMessage(room, message.MessageId, message.ThreadId, message.Text, message.MessageUrl);
    }

    public OutputsBuilder SetMessage(
        Room? room,
        string? ts,
        string? threadTs,
        string? text,
        Uri? url)
    {
        if (room is not null && ts is not null)
        {
            Outputs["message"] = new MessageOutput
            {
                Channel = ChannelOutput.FromRoom(room),
                Timestamp = ts,
                ThreadTimestamp = threadTs,
                Text = text,
                Url = url,
            };
        }
        return this;
    }

    public OutputsBuilder SetSelectionResponse(string? value, string label)
    {
        Outputs["selection_response"] = new SelectedOption
        {
            Value = value ?? label,
            Label = label,
        };
        return this;
    }

    public OutputsBuilder SetPollResponse(string? value, string label)
    {
        Outputs["poll_response"] = new SelectedOption
        {
            Value = value ?? label,
            Label = label,
        };
        return this;
    }

    public OutputsBuilder SetInvitationResponse(string value, string? teamId, ActorOutput? actor)
    {
        Outputs["invitation_response"] = value;
        if (teamId is null)
        {
            Outputs["team"] = new { id = teamId };
        }

        if (actor is not null)
        {
            Outputs["actor"] = actor;
        }

        return this;
    }

    public OutputsBuilder SetActor(Member member)
    {
        Outputs["actor"] = ActorOutput.FromMember(member);
        return this;
    }
}
