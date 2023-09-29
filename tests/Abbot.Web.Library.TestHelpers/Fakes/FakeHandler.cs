using System;
using System.Threading.Tasks;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Abbot.Skills;
using Serious.Payloads;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeHandler : IHandler
{
    public Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
    {
        OnSubmissionAsyncCalled = true;
        return Task.CompletedTask;
    }

    public bool OnSubmissionAsyncCalled { get; set; }

    public Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext)
    {
        OnInteractionAsyncCalled = true;
        return Task.CompletedTask;
    }

    public bool OnInteractionAsyncCalled { get; set; }

    public Task OnClosedAsync(IViewContext<IViewClosedPayload> viewContext)
    {
        OnClosedAsyncCalled = true;
        return Task.CompletedTask;
    }

    public bool OnClosedAsyncCalled { get; set; }

    public Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
    {
        OnMessageInteractionCalled = true;
        return Task.CompletedTask;
    }

    public bool OnMessageInteractionCalled { get; set; }

    public Task<BlockSuggestionsResponse> OnBlockSuggestionRequestAsync(IPlatformEvent<BlockSuggestionPayload> platformEvent)
    {
        OnBlockSuggestionRequestCalled = true;
        return Task.FromResult<BlockSuggestionsResponse>(new OptionsBlockSuggestionsResponse(Array.Empty<Option>()));
    }

    public bool OnBlockSuggestionRequestCalled { get; set; }
}
