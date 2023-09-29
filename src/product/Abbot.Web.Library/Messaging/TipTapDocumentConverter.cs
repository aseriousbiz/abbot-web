using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Logging;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// Converts nodes of a TipTap document into their respective node types.
/// </summary>
public class TipTapDocumentConverter : JsonConverter<TipTapDocument>
{
    static readonly ILogger<TipTapDocumentConverter> Log = ApplicationLoggerFactory.CreateLogger<TipTapDocumentConverter>();

    public override TipTapDocument? ReadJson(
        JsonReader reader,
        Type objectType,
        TipTapDocument? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType is not JsonToken.StartObject)
        {
            return null;
        }

        var obj = JObject.Load(reader);
        var deserialized = DeserializeToken(obj);
        return deserialized as TipTapDocument;
    }

    static IEnumerable<TipTapNode> EnumerateNodes(JArray tokenArray)
        => tokenArray.Select(DeserializeToken).WhereNotNull();

    static TipTapNode? DeserializeToken(JToken token) =>
        token["type"]?.Value<string>() switch
        {
            "doc" => CreateContentNode<TipTapDocument>(token),
            "paragraph" => CreateContentNode<TipTapParagraphNode>(token),
            "blockquote" => CreateContentNode<TipTapBlockQuoteNode>(token),
            "codeBlock" => CreateCodeBlockNode(token),
            "emoji" => CreateTipTapAttributeNode<TipTapEmojiNode>(token),
            "mention" => CreateTipTapAttributeNode<TipTapUserMentionNode>(token),
            "channel" => CreateTipTapAttributeNode<TipTapChannelMentionNode>(token),
            "text" => CreateTextNode(token),
            "handlebars" => CreateTipTapAttributeNode<TipTapHandlebarsNode>(token),
            "listItem" => CreateListItemNode(token),
            "bulletList" => CreateListNode<TipTapBulletListNode>(token),
            "orderedList" => CreateListNode<TipTapOrderedListNode>(token),
            null => null,
            // Must be a mark
            var type => new TipTapMarkNode(type),
        };

    static TContentNode? CreateContentNode<TContentNode>(JToken token)
        where TContentNode : TipTapContentNode, new()
    {
        return token["content"] is JArray content
            ? new TContentNode
            {
                Content = EnumerateNodes(content).ToReadOnlyList(),
            }
            : null;
    }

    static TipTapCodeBlockNode? CreateCodeBlockNode(JToken token)
    {
        return token["content"] is JArray content
            ? new TipTapCodeBlockNode
            {
                Content = content.Select(CreateTextNode).WhereNotNull().ToReadOnlyList(),
            }
            : null;
    }

    static TContentNode? CreateListNode<TContentNode>(JToken token)
        where TContentNode : TipTapContentNode<TipTapListItemNode>, new()
    {
        return token["content"] is JArray content
            ? new TContentNode
            {
                Content = content.Select(CreateListItemNode).ToReadOnlyList(),
            }
            : null;
    }

    static TipTapListItemNode CreateListItemNode(JToken token)
    {
        var paragraphContent = GetListItemNodeContent(token);

        return new TipTapListItemNode
        {
            Content = new[]
            {
                new TipTapParagraphNode
                {
                    Content = paragraphContent.ToReadOnlyList(),
                }
            }
        };
    }

    static IEnumerable<TipTapNode> GetListItemNodeContent(JToken token)
    {
        if (token["content"] is not JArray { Count: > 0 } content)
        {
            return Enumerable.Empty<TipTapNode>();
        }

        if (content is { Count: > 1 })
        {
            Log.ListItemContainsMoreThanOneParagraph();
            // If there's more than one, let's just return the first one and ignore the remainders.
        }

        var paragraphToken = content[0];

        return paragraphToken["type"]?.Value<string>() is "paragraph"
               && paragraphToken["content"] is JArray paragraphContentTokens
            ? EnumerateNodes(paragraphContentTokens)
            : Enumerable.Empty<TipTapNode>();
    }

    static TNode? CreateTipTapAttributeNode<TNode>(JToken token) where TNode : TipTapNodeWithAttributes, new()
    {
        return token["attrs"]?.ToObject<TipTapAttributes>() is { } attributes
            ? new TNode
            {
                Attributes = attributes,
                Marks = ReadMarks(token).ToReadOnlyList(),
            }
            : null;
    }

    static TipTapTextNode? CreateTextNode(JToken node)
    {
        if (node["text"]?.Value<string>() is not { } text)
        {
            return null;
        }

        var marks = ReadMarks(node);

        return new TipTapTextNode
        {
            Text = text,
            Marks = marks.ToReadOnlyList(),
        };
    }

    static IEnumerable<TipTapMarkNode> ReadMarks(JToken node)
    {
        if (node["marks"] is not JArray markTokens)
        {
            yield break;
        }

        foreach (var markToken in markTokens)
        {
            if (markToken["type"]?.Value<string>() is { Length: > 0 } type)
            {
                var mark = type is "link"
                    ? markToken["attrs"]?.ToObject<TipTapLinkAttributes>() is { } linkAttributes
                        ? new TipTapLinkMark
                        {
                            Attributes = linkAttributes,
                        }
                        : null
                    : new TipTapMarkNode(type);

                if (mark is not null)
                {
                    yield return mark;
                }
            }
        }
    }

    /// <summary>
    /// Use default behavior for writing.
    /// </summary>
    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, TipTapDocument? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

static partial class TipTapDocumentConverterLoggingExtensions
{
    // NOTE: We don't want to log the node contents since this is user data. The best we can do
    // is make sure we're in a logging context with enough information.
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "A TipTap List Item contained more than one paragraph.")]
    public static partial void ListItemContainsMoreThanOneParagraph(this ILogger<TipTapDocumentConverter> logger);
}
