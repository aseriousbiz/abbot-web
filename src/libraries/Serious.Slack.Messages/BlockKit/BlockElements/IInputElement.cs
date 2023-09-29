using Serious.Slack.BlockKit;

namespace Serious.Slack.Abstractions;

/// <summary>
/// Interface for types that collect information from users. These can be used for the <c>element</c> property
/// of an <c>input</c> block (<see cref="Input"/>). Effectively every interactive element other than <c>button</c>.
/// </summary>
public interface IInputElement : IPayloadElement
{
}

/// <summary>
/// Interface for types that can be used within the <c>elements</c> of a <c>context</c> block
/// (see <see cref="Context"/>).
/// </summary>
public interface IContextBlockElement : IElement
{
}
