using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// The root of a <see href="https://tiptap.dev/">TipTap</see> JSON document.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(TipTapDocumentConverter))]
public record TipTapDocument() : TipTapContentNode("doc")
{
    public TipTapDocument(IEnumerable<TipTapNode> content) : this()
    {
        Content = content.ToList();
    }
}

/// <summary>
/// A JSON node in a TipTap JSON document.
/// </summary>
/// <param name="Type">The type of the TipTap node.</param>
public abstract record TipTapNode(
    [property: JsonPropertyName("type")]
    [property: JsonProperty("type")]
    string Type);

/// <summary>
/// As JSON paragraph node in a TipTap JSON document.
/// </summary>
public record TipTapParagraphNode() : TipTapContentNode("paragraph")
{
    public TipTapParagraphNode(IEnumerable<TipTapNode> content) : this()
    {
        Content = content.ToList();
    }
}

public abstract record TipTapContentNode(string Type) : TipTapContentNode<TipTapNode>(Type);

/// <summary>
/// Base type for a node that contains content.
/// </summary>
public abstract record TipTapContentNode<TNode>(string Type) : TipTapNode(Type) where TNode : TipTapNode
{
    /// <summary>
    /// The content of the node.
    /// </summary>
    [JsonPropertyName("content")]
    [JsonProperty("content")]
    public IReadOnlyList<TNode> Content { get; init; } = null!;
}

/// <summary>
/// A TipTap text node that may include optional marks such as bold, italic, etc.
/// </summary>
public record TipTapTextNode() : TipTapMarkableNode("text")
{
    public TipTapTextNode(string text, params TipTapMarkNode[] marks) : this()
    {
        Text = text;
        Marks = marks;
    }

    /// <summary>
    /// The text of the node.
    /// </summary>
    [JsonPropertyName("text")]
    [JsonProperty("text")]
    public string Text { get; init; } = null!;
}

public abstract record TipTapMarkableNode(string Type) : TipTapNode(Type)
{
    /// <summary>
    /// The formatting marks for the node.
    /// </summary>
    [JsonPropertyName("marks")]
    [JsonProperty("marks")]
    public IReadOnlyList<TipTapNode> Marks { get; init; } = Array.Empty<TipTapNode>();

    public bool ShouldSerializeMarks()
    {
        // Don't serialize empty marks.
        return Marks is { Count: > 0 };
    }
}

/// <summary>
/// A block quote.
/// </summary>
public record TipTapBlockQuoteNode() : TipTapContentNode("blockquote");

/// <summary>
/// A block of code.
/// </summary>
public record TipTapCodeBlockNode() : TipTapContentNode<TipTapTextNode>("codeBlock");

/// <summary>
/// A list item with in a list node.
/// </summary>
public record TipTapListItemNode() : TipTapContentNode<TipTapParagraphNode>("listItem")
{
    /// <summary>
    /// A list item can only contain a single paragraph node, but it's still represented as an array.
    /// </summary>
    public IReadOnlyList<TipTapNode> ParagraphContent => Content.Single().Content;
};

/// <summary>
/// A bulleted list node in a TipTap JSON document.
/// </summary>
public record TipTapBulletListNode() : TipTapContentNode<TipTapListItemNode>("bulletList");

/// <summary>
/// An ordered list node in a TipTap JSON document.
/// </summary>
public record TipTapOrderedListNode() : TipTapContentNode<TipTapListItemNode>("orderedList");

/// <summary>
/// Supplies the attributes for the Mention plugin.
/// </summary>
/// <remarks>
/// These are the attributes recognized by the tip tap mention plugin's data model. It's probably possible to
/// extend these, but I haven't spent the time to figure it out. -@haacked.
/// </remarks>
/// <param name="Id">The Id of the mention.</param>
/// <param name="Label">The label of the mention.</param>
/// <param name="Context">Optional additional context.</param>
public record TipTapAttributes(
    [property: JsonPropertyName("id")]
    [property: JsonProperty("id")]
    string Id,

    [property: JsonPropertyName("label")]
    [property: JsonProperty("label")]
    string Label,

    [property: JsonPropertyName("context")]
    [property: JsonProperty("context")]
    string? Context = null);

public record TipTapChannelMentionNode() : TipTapNodeWithAttributes("channel")
{
    public TipTapChannelMentionNode(TipTapAttributes attributes) : this()
    {
        Attributes = attributes;
    }
}

public record TipTapUserMentionNode() : TipTapNodeWithAttributes("mention")
{
    public TipTapUserMentionNode(TipTapAttributes attributes) : this()
    {
        Attributes = attributes;
    }
}

public record TipTapEmojiNode() : TipTapNodeWithAttributes("emoji")
{
    public TipTapEmojiNode(TipTapAttributes attributes) : this()
    {
        Attributes = attributes;
    }
}

public record TipTapHandlebarsNode() : TipTapNodeWithAttributes("handlebars")
{
    public TipTapHandlebarsNode(TipTapAttributes attributes) : this()
    {
        Attributes = attributes;
    }
}

public record TipTapLinkMark() : TipTapMarkNode("link")
{
    public TipTapLinkMark(TipTapLinkAttributes attributes) : this()
    {
        Attributes = attributes;
    }

    [JsonPropertyName("attrs")]
    [JsonProperty("attrs")]
    public TipTapLinkAttributes Attributes { get; init; } = null!;
};

public record TipTapLinkAttributes(
    [property:JsonPropertyName("href")]
    [property:JsonProperty("href")]
    string Href);

public abstract record TipTapNodeWithAttributes(string Type) : TipTapMarkableNode(Type)
{
    [JsonPropertyName("attrs")]
    [JsonProperty("attrs")]
    public TipTapAttributes Attributes { get; init; } = null!;
}

public record TipTapMarkNode(string Type) : TipTapNode(Type);

public static class HandlebarsNodeExtensions
{
    public static string GetTemplate(this TipTapHandlebarsNode node)
    {
        return "{{" + node.Attributes.Id + "}}";
    }
}
