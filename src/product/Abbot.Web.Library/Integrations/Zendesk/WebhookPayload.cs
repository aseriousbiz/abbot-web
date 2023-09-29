namespace Serious.Abbot.Integrations.Zendesk;

public record WebhookPayload(
    string TicketUrl,
    long TicketId,
    string? TicketStatus = null,
    long? CurrentUserId = null)
{
    public static readonly string Template = @"{
    ""TicketUrl"": ""{{ticket.url}}"",
    ""TicketId"": {{ticket.id}},
    ""TicketStatus"": ""{{ticket.status}}"",
    ""CurrentUserId"": {{current_user.id}}
}";
}
