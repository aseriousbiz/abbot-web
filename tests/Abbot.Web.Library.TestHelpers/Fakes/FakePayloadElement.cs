using Serious.Slack.BlockKit;

namespace Abbot.Common.TestHelpers.Fakes;

public static class FakePayloadElement
{
    /// <summary>
    /// Create a <see cref="IPayloadElement"/> derived class with the block id and action id set. We need to it
    /// this way because we don't want these properties settable by users, but we need to set it. I'll come
    /// up with something better later.
    /// </summary>
    public static IPayloadElement Create<T>(T element, string blockId, string? actionId = null) where T : IPayloadElement
    {
        return new FakePayloadElement<T>(element, blockId, actionId).Element;
    }
}

public class FakePayloadElement<T> where T : IPayloadElement
{
    public FakePayloadElement(T element, string blockId, string? actionId = null)
    {
        // Hack because we don't want these properties settable by users, but we need them to be for our tests.
        // I'll come up with something better later.
        typeof(IPayloadElement).GetProperty(nameof(IPayloadElement.BlockId))!.SetValue(element, blockId);
        element.GetType().GetProperty(nameof(IPayloadElement.ActionId))!.SetValue(element, actionId);
        Element = element;
    }

    public T Element { get; }
}
