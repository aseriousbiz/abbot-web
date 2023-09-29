using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Schema;

namespace Serious.TestHelpers
{
    public class FakeTurnContext : ITurnContext<IMessageActivity>
    {
        readonly ITurnContext<IMessageActivity> _turnContext;

        readonly List<IActivity> _sentActivities = new();
        readonly List<IActivity> _updateActivities = new();

        public FakeTurnContext() : this(new Activity())
        {
        }

        public FakeTurnContext(Activity activity)
        {
            Adapter = new TestAdapter();
            _turnContext = new DelegatingTurnContext<IMessageActivity>(new TurnContext(Adapter, activity));
        }

        public IReadOnlyList<IActivity> SentActivities => _sentActivities;
        public IReadOnlyList<IActivity> UpdateActivities => _updateActivities;

        public ConversationReference? DeletedConversationReference { get; private set; }

        public async Task SendActivityAsync(Activity activity)
        {
            await SendActivitiesAsync(new IActivity[] { activity }, CancellationToken.None);
        }

        public Task<ResourceResponse> SendActivityAsync(
            string textReplyToSend,
            string? speak = null,
            string inputHint = "acceptingInput",
            CancellationToken cancellationToken = new())
        {
            return _turnContext.SendActivityAsync(textReplyToSend, speak, inputHint, cancellationToken);
        }

        public Task<ResourceResponse> SendActivityAsync(
            IActivity activity,
            CancellationToken cancellationToken = new())
        {
            _sentActivities.Add(activity);
            return _turnContext.SendActivityAsync(activity, cancellationToken);
        }

        public Task<ResourceResponse[]> SendActivitiesAsync(
            IActivity[] activities,
            CancellationToken cancellationToken = new())
        {
            _sentActivities.AddRange(activities);
            return _turnContext.SendActivitiesAsync(activities, cancellationToken);
        }

        public Task<ResourceResponse> UpdateActivityAsync(
            IActivity activity,
            CancellationToken cancellationToken = new())
        {
            _updateActivities.Add(activity);
            return _turnContext.UpdateActivityAsync(activity, cancellationToken);
        }

        public Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = new())
        {
            return DeleteActivityAsync(new ConversationReference { ActivityId = activityId }, cancellationToken);
        }

        public Task DeleteActivityAsync(
            ConversationReference conversationReference,
            CancellationToken cancellationToken = new())
        {
            DeletedConversationReference = conversationReference;
            return _turnContext.DeleteActivityAsync(conversationReference, cancellationToken);
        }

        public ITurnContext OnSendActivities(SendActivitiesHandler handler)
        {
            return _turnContext.OnSendActivities(handler);
        }

        public ITurnContext OnUpdateActivity(UpdateActivityHandler handler)
        {
            return _turnContext.OnUpdateActivity(handler);
        }

        public ITurnContext OnDeleteActivity(DeleteActivityHandler handler)
        {
            return _turnContext.OnDeleteActivity(handler);
        }

        public BotAdapter Adapter { get; }

        public TurnContextStateCollection TurnState => _turnContext.TurnState;

        public IMessageActivity Activity => (Activity)_turnContext.Activity;

        Activity ITurnContext.Activity => (Activity)_turnContext.Activity;

        public bool Responded => true;
    }
}
