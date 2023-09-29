using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Serious.Logging;

namespace Serious.Abbot.Playbooks;

public static class TipTapDocumentExtensions
{
    /// <summary>
    /// Converts the <see cref="TipTapDocument"/> into a set of Block Kit blocks.
    /// </summary>
    /// <param name="document">The <see cref="TipTapDocument"/> to render.</param>
    /// <param name="templateEvaluator">A template evaluator used to evaluate handlebar expressions against Inputs.</param>
    /// <returns></returns>
    public static string ToMrkdwn(this TipTapDocument document, ITemplateEvaluator? templateEvaluator)
    {
        return new TipTapJsonParser().Parse(document, templateEvaluator);
    }
}

public class TipTapJsonParser
{
    static readonly ILogger<TipTapJsonParser> Log = ApplicationLoggerFactory.CreateLogger<TipTapJsonParser>();

    public string Parse(TipTapDocument document, ITemplateEvaluator? templateEvaluator)
    {
        var visitor = new TipTapJsonVisitor(templateEvaluator);
        VisitDocument(document, visitor);
        return visitor.Results.ToString().TrimEnd();
    }

    void Visit(TipTapNode node, TipTapNode? nextNode, TipTapJsonVisitor visitor)
    {
        Action visitMethod = node switch
        {
            TipTapHandlebarsNode handlebarsNode => () => VisitHandlebars(handlebarsNode, nextNode, visitor),
            TipTapTextNode textNode => () => VisitText(textNode, nextNode, visitor),
            TipTapParagraphNode paragraphNode => () => VisitParagraph(paragraphNode, visitor),
            TipTapUserMentionNode mentionNode => () => VisitMention(mentionNode, nextNode, visitor),
            TipTapChannelMentionNode channelNode => () => VisitChannel(channelNode, nextNode, visitor),
            TipTapEmojiNode emojiNode => () => VisitEmoji(emojiNode, nextNode, visitor),
            _ => () => { Log.UnexpectedNodeType(node.Type, node.GetType().Name); }
        };

        visitMethod();
    }

    void VisitBlockQuote(TipTapBlockQuoteNode blockQuoteNode, TipTapJsonVisitor visitor)
    {
        foreach (var child in blockQuoteNode.Content)
        {
            Action action = child switch
            {
                TipTapBlockQuoteNode nestedBlockQuote => () => {
                    // Running into more Slack mrkdwn problems. If we add a space after this >, Slack won't render
                    // the initial block quote. No matter what we do, it doesn't render the nested block quote,
                    // even though if we paste the same text into Slack, it renders fine.
                    visitor.Append(">");
                    VisitBlockQuote(nestedBlockQuote, visitor);
                }
                ,
                TipTapParagraphNode paragraphNode => () => {
                    visitor.Append("> ");
                    VisitParagraph(paragraphNode, visitor);
                }
                ,
                TipTapBulletListNode bulletListNode => () => VisitBulletList(bulletListNode, visitor, inBlockQuote: true),
                TipTapOrderedListNode orderedListNode => () => VisitOrderedList(orderedListNode, visitor, inBlockQuote: true),
                TipTapCodeBlockNode codeBlockNode => () => VisitCodeBlock(codeBlockNode, visitor, inBlockQuote: true),
                _ => () => {
                    Log.UnexpectedNodeType(child.Type, child.GetType().Name);
                }
                ,
            };

            action();
        }
    }

    static void VisitCodeBlock(TipTapCodeBlockNode codeBlockNode, TipTapJsonVisitor visitor, bool inBlockQuote = false)
    {
        if (inBlockQuote)
        {
            visitor.Append("> ");
        }
        visitor.Append("```");
        foreach (var textNode in codeBlockNode.Content)
        {
            visitor.Append(textNode.Text.Replace("\\n", "\n", StringComparison.Ordinal));
        }
        visitor.Append("```");
        visitor.Append("\n");
    }

    void VisitDocument(TipTapDocument document, TipTapJsonVisitor visitor)
    {
        foreach (var child in document.Content)
        {
            Action action = child switch
            {
                TipTapParagraphNode paragraphNode => () => VisitParagraph(paragraphNode, visitor),
                TipTapBulletListNode bulletListNode => () => VisitBulletList(bulletListNode, visitor),
                TipTapOrderedListNode orderedListNode => () => VisitOrderedList(orderedListNode, visitor),
                TipTapBlockQuoteNode blockQuoteNode => () => VisitBlockQuote(blockQuoteNode, visitor),
                TipTapCodeBlockNode codeBlockNode => () => VisitCodeBlock(codeBlockNode, visitor),
                _ => () => {
                    Log.UnexpectedNodeType(child.Type, child.GetType().Name);
                }
                ,
            };

            action();
        }
    }

    static void VisitHandlebars(TipTapHandlebarsNode handlebarsNode, TipTapNode? nextNode, TipTapJsonVisitor visitor)
    {
        visitor.BeginMarks(handlebarsNode);
        visitor.EvaluateAndAppend(handlebarsNode.GetTemplate());
        visitor.EndMarks(nextNode);
    }

    static void VisitText(TipTapTextNode textNode, TipTapNode? nextNode, TipTapJsonVisitor visitor)
    {
        var text = textNode.Text;

        // Marks need to be next to the text. So if we get text like " text" we want to render the space
        // before we render the mark.
        var (leadingTrimmed, leadingSpaces) = text.TrimLeadingWhitespace();
        // We also need to handle trailing spaces like "text " to ensure the ending mark next to the text.
        var (trailingTrimmed, trailingSpaces) = leadingTrimmed.TrimTrailingWhitespace();
        if (leadingSpaces > 0)
        {
            visitor.Append(new string(' ', leadingSpaces));
            text = trailingTrimmed;
        }

        if (trailingSpaces > 0)
        {
            text = trailingTrimmed;
        }
        visitor.BeginMarks(textNode);
        visitor.EvaluateAndAppend(text);
        visitor.EndMarks(nextNode);
        if (trailingSpaces > 0)
        {
            visitor.Append(new string(' ', trailingSpaces));
        }
    }

    void VisitParagraph(TipTapParagraphNode paragraphNode, TipTapJsonVisitor visitor)
    {
        foreach (var (child, nextChild) in paragraphNode.Content.SelectWithNext())
        {
            Visit(child, nextChild, visitor);
        }
        visitor.Append("\n");
    }

    static void VisitMention(TipTapUserMentionNode userNode, TipTapNode? nextNode, TipTapJsonVisitor visitor)
    {
        visitor.BeginMarks(userNode);
        visitor.Append("<@");
        visitor.EvaluateAndAppend(userNode.Attributes.Id);
        visitor.Append(">");
        visitor.EndMarks(nextNode);
    }

    static void VisitChannel(TipTapChannelMentionNode channelNode, TipTapNode? nextNode, TipTapJsonVisitor visitor)
    {
        visitor.BeginMarks(channelNode);
        visitor.Append("<#");
        visitor.EvaluateAndAppend(channelNode.Attributes.Id);
        visitor.Append(">");
        visitor.EndMarks(nextNode);
    }

    static void VisitEmoji(TipTapEmojiNode emojiNode, TipTapNode? nextNode, TipTapJsonVisitor visitor)
    {
        visitor.BeginMarks(emojiNode);
        visitor.Append(":");
        visitor.EvaluateAndAppend(emojiNode.Attributes.Id);
        visitor.Append(":");
        visitor.EndMarks(nextNode);
    }

    void VisitBulletList(TipTapBulletListNode bulletListNode, TipTapJsonVisitor visitor, bool inBlockQuote = false)
    {
        // The API doesn't seem to like nested bullet list, but it works fine via the Slack UI. Weird.
        Func<int, string> itemMark = inBlockQuote ? _ => "> • " : _ => "* ";
        VisitList(itemMark, bulletListNode, visitor);
    }

    void VisitOrderedList(TipTapOrderedListNode bulletListNode, TipTapJsonVisitor visitor, bool inBlockQuote = false)
    {
        Func<int, string> itemMark = inBlockQuote ? number => $"> {number}. " : number => $"{number}. ";
        VisitList(itemMark, bulletListNode, visitor);
    }

    void VisitList(Func<int, string> itemMark, TipTapContentNode<TipTapListItemNode> listNode, TipTapJsonVisitor visitor)
    {
        int index = 0;
        foreach (var listItemNode in listNode.Content)
        {
            visitor.Append(itemMark(++index));
            // List items can only have a single paragraph child.
            // We ensure that in the TipTapDocumentConverter.
            if (listItemNode.Content is [var paragraphNode])
            {
                VisitParagraph(paragraphNode, visitor);
            }
        }
    }

    class TipTapJsonVisitor
    {
        readonly ITemplateEvaluator? _templateEvaluator;

        public TipTapJsonVisitor(ITemplateEvaluator? templateEvaluator)
        {
            _templateEvaluator = templateEvaluator;
        }

        List<TipTapNode> _currentMarks = new();

        public StringBuilder Results { get; } = new();

        readonly Dictionary<string, string> _marksLookup = new()
        {
            { "bold", "*" },
            { "italic", "_" },
            { "strike", "~" },
            { "code", "`" },
            { "link", "<>" }
        };

        public void Append(string s)
        {
            Results.Append(s);
        }

        public void EvaluateAndAppend(string template)
        {
            Results.Append(_templateEvaluator?.Evaluate(template) ?? template);
        }

        public void BeginMarks(TipTapNode node)
        {
            var newMarks = NewMarks(node);

            foreach (var mark in newMarks)
            {
                var markChar = GetBeginningMarkChar(mark.Type);
                Append(markChar);
                if (mark is TipTapLinkMark linkMark)
                {
                    var href = linkMark.Attributes.Href;
                    href = _templateEvaluator?.Evaluate(href) as string ?? href;
                    Append($"{href}|");
                }
            }

            _currentMarks.AddRange(newMarks);
        }

        public void EndMarks(TipTapNode? nextNode = null)
        {
            var closingMarks = EndedMarks(nextNode);
            closingMarks.Reverse();
            foreach (var mark in closingMarks)
            {
                Append(GetEndingMarkChar(mark.Type));
            }

            _currentMarks = _currentMarks.Where(mark => !closingMarks.Contains(mark)).ToList();
        }

        string GetBeginningMarkChar(string mark)
        {
            return _marksLookup.TryGetValue(mark, out var markChars)
                ? markChars[0].ToString()
                : "";
        }

        string GetEndingMarkChar(string mark)
        {
            return _marksLookup.TryGetValue(mark, out var markChars)
                ? markChars[^1].ToString()
                : "";
        }

        List<TipTapNode> NewMarks(TipTapNode node)
        {
            if (node is not TipTapMarkableNode markable || !markable.Marks.Any())
            {
                return new List<TipTapNode>();
            }

            return ValidMarks(markable).Except(_currentMarks).ToList();
        }

        List<TipTapNode> EndedMarks(TipTapNode? nextNode)
        {
            if (nextNode is not TipTapMarkableNode markable || !markable.Marks.Any())
            {
                return _currentMarks.ToList();
            }

            return _currentMarks.Except(ValidMarks(markable)).ToList();
        }

        static IEnumerable<TipTapNode> ValidMarks(TipTapMarkableNode markable) =>
            markable.Marks
                .Where(m => m switch {
                    TipTapLinkMark => markable switch
                    {
                        TipTapChannelMentionNode => false,
                        TipTapUserMentionNode => false,
                        TipTapEmojiNode => false,
                        _ => true,
                    },
                    _ => true,
                });
    }
}

static partial class TipTapJsonParserLoggingExtensions
{
    // NOTE: We don't want to log the node contents since this is user data. The best we can do
    // is make sure we're in a logging context with enough information.
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Unexpected TipTap Node {Type} with CLR type {ClrType}.")]
    public static partial void UnexpectedNodeType(this ILogger<TipTapJsonParser> logger, string type, string clrType);
}
