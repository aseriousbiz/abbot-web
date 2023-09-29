using Microsoft.Bot.Schema;
using Serious.Abbot.Messaging;
using Serious.Slack;
using Serious.Slack.BotFramework;
using Serious.TestHelpers;

public class FakeResponder : IResponder
{
    // Eventually, we want to make this class a real responder talking to a fake TurnContext.
    // For now, we'll just use a real one in a few places places.
    readonly FakeTurnContext _fakeTurnContext = new();
    readonly IResponder _innerResponder;

    readonly List<IMessageActivity> _sentMessages = new();
    readonly List<Uri> _deletedMessagesByResponseUrlByUri = new();
    readonly Dictionary<string, ViewUpdatePayload> _modals = new();
    readonly List<(string TriggerId, ViewUpdatePayload Payload)> _pushedModals = new();

    public IEnumerable<IMessageActivity> SentMessages => _sentMessages;

    public IEnumerable<Uri> DeletedMessagesByResponseUrl => _deletedMessagesByResponseUrlByUri;

    public IReadOnlyDictionary<string, ViewUpdatePayload> OpenModals => _modals;
    public IReadOnlyList<(string, ViewUpdatePayload)> PushedModals => _pushedModals;

    public IMessageActivity FirstActivityReply() => SentMessages.First();

    public IMessageActivity LastActivityReply() => SentMessages.Last();

    public IMessageActivity SingleActivityReply() => SentMessages.Single();

    public ResponseAction? ResponseAction { get; private set; }

    public string SingleReply() => SingleActivityReply().Text;

    public FakeResponder()
    {
        _innerResponder = new Responder(new FakeSimpleSlackApiClient(), _fakeTurnContext);
    }

    public IReadOnlyDictionary<string, string>? ValidationErrors =>
        _fakeTurnContext.TurnState.TryGetValue(ActivityResult.ResponseBodyKey, out var errors)
        && errors is ErrorResponseAction errorResponse
            ? errorResponse.Errors
            : null;

    public Task SendActivityAsync(IMessageActivity message, IMessageTarget? messageTarget = null)
    {
        if (messageTarget is not null)
        {
            message.OverrideDestination(messageTarget);
        }

        _sentMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task UpdateActivityAsync(IMessageActivity message)
    {
        var existing = message.Id is not null
            ? _sentMessages.FirstOrDefault(a => a.Id == message.Id)
            : null;
        if (existing != null)
        {
            _sentMessages.Remove(existing);
        }

        _sentMessages.Add(message);
        return Task.CompletedTask;
    }

    public void ReportValidationErrors(IReadOnlyDictionary<string, string> errors)
    {
        _innerResponder.ReportValidationErrors(errors);
    }

    public void SetResponseAction(ResponseAction action)
    {
        ResponseAction = action;
    }

    public void SetJsonResponse(object responseBody)
    {
        _fakeTurnContext.TurnState[ActivityResult.ResponseBodyKey] = responseBody;
    }

    public bool HasValidationErrors => _innerResponder.HasValidationErrors;

    public Task DeleteActivityAsync(string platformRoomId, string activityId)
    {
        var existing = _sentMessages.FirstOrDefault(a => a.Id == activityId);
        if (existing != null)
        {
            _sentMessages.Remove(existing);
        }

        return Task.CompletedTask;
    }

    public Task DeleteActivityAsync(Uri responseUrl)
    {
        _deletedMessagesByResponseUrlByUri.Add(responseUrl);
        return Task.CompletedTask;
    }

    public async Task<ViewResponse> OpenModalAsync(string triggerId, ViewUpdatePayload view)
    {
        _modals.Add(triggerId, view);
        return new ViewResponse
        {
            Ok = true,
            Body = new ModalView { Id = "V0939292" }
        };
    }

    public async Task<ViewResponse> PushModalAsync(string triggerId, ViewUpdatePayload view)
    {
        _pushedModals.Add((triggerId, view));
        return new ViewResponse
        {
            Ok = true,
            Body = new ModalView { Id = "V0121344" }
        };
    }

    public async Task<ViewResponse> UpdateModalAsync(string viewId, ViewUpdatePayload view)
    {
        _modals.Add(viewId, view);
        return new ViewResponse
        {
            Ok = true,
            Body = new ModalView { Id = viewId }
        };
    }
}
