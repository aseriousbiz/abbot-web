using Serious.Abbot.Live;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeFlashPublisher : IFlashPublisher
{
    readonly List<(FlashName Name, FlashGroup Group, object[] Arguments)> _publishedFlashes = new();

    public IReadOnlyList<(FlashName Name, FlashGroup Group, object[] Arguments)> PublishedFlashes => _publishedFlashes;

    public Task PublishAsync(FlashName flash, FlashGroup group, params object[] arguments)
    {
        _publishedFlashes.Add((flash, group, arguments));
        return Task.CompletedTask;
    }
}
