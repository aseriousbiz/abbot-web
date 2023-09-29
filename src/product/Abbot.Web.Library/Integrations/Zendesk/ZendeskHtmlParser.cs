using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Integrations.Zendesk;

public static class ZendeskHtmlParser
{
    public static IEnumerable<ILayoutBlock> ParseHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var mrkdwn = ParseHtmlNode(doc.DocumentNode);
        yield return new Section(new MrkdwnText(mrkdwn));
    }

    static string ParseHtmlNode(HtmlNode node)
    {
        var builder = new MrkdwnBuilder();
        VisitChildNodes(node, builder);
        return builder.ToString();
    }

    static void VisitChildNodes(HtmlNode node, MrkdwnBuilder builder)
    {
        if (node.Name is "#text")
        {
            builder.Append(node.InnerText.Replace("&nbsp;", " ", StringComparison.Ordinal));
        }

        using var parentTag = MrkdwnTag.GetTag(node, builder);
        foreach (var child in node.ChildNodes)
        {
            VisitChildNodes(child, parentTag?.Builder ?? builder);
        }
    }
}

public record MrkdwnBuilder
{
    readonly StringBuilder _sb = new StringBuilder();

    public ParseState ParseState { get; init; }

    public int Indentation { get; init; }

    public void Append(string text) => _sb.Append(text);

    public override string ToString() => _sb.ToString();

    static readonly Regex IndentRegex = new(@"margin-left: (?<pixels>\d+)px", RegexOptions.Compiled);

    public int OrderedListItemNumber { get; set; }

    public MrkdwnBuilder Indent(HtmlNode node)
    {
        var style = node.Attributes["style"]?.Value;
        if (style is null)
            return this;

        var match = IndentRegex.Match(style);
        if (!match.Success || !int.TryParse(match.Groups["pixels"].Value, out var pixels))
            return this;

        return this with
        {
            ParseState = ParseState.Indented,
            Indentation = pixels / 5
        };
    }
}

public enum ParseState
{
    None,
    InPreBlock,
    InBlockquote,
    InUnorderedList,
    InOrderedList,
    Indented,
}

public class MrkdwnTag : IDisposable
{
    public const char Space = ' ';
    readonly string _endDelimiter;

    public MrkdwnBuilder Builder { get; }

    public static MrkdwnTag? GetTag(HtmlNode node, MrkdwnBuilder builder)
    {
        string Indent()
        {
            return builder.Indentation > 1
                ? new string(Space, builder.Indentation)
                : string.Empty;
        }
        return node.Name.ToLowerInvariant() switch
        {
            "code" when builder.ParseState is ParseState.InPreBlock
                => new MrkdwnTag("```\n", "\n```", builder),
            "code" => new MrkdwnTag("`", builder),
            "strong" or "b" => new MrkdwnTag("*", builder),
            "em" or "i" => new MrkdwnTag("_", builder),
            "br" => new MrkdwnTag("\n", "", builder),
            "a" => new MrkdwnTag($"<{node.Attributes["href"].Value}|", ">", builder),
            "pre" => new MrkdwnTag("", builder with { ParseState = ParseState.InPreBlock }),
            "blockquote" => new MrkdwnTag("", builder with { ParseState = ParseState.InBlockquote }),
            "p" when builder.ParseState is ParseState.InBlockquote
                => new MrkdwnTag("> ", endDelimiter: "\n", builder),
            "p" when builder.ParseState is ParseState.Indented
                => new MrkdwnTag(Indent(), endDelimiter: "\n", builder),
            "p" => new MrkdwnTag("", "\n", builder),
            "ul" when builder.ParseState is ParseState.InOrderedList or ParseState.InUnorderedList
                => new MrkdwnTag(
                    startDelimiter: "",
                    endDelimiter: "",
                    builder with
                    {
                        Indentation = builder.Indentation + 4,
                        ParseState = ParseState.InUnorderedList,
                        OrderedListItemNumber = 0,
                    }),
            "ol" when builder.ParseState is ParseState.InOrderedList or ParseState.InUnorderedList
                => new MrkdwnTag(
                    startDelimiter: "",
                    endDelimiter: "",

                    builder with
                    {
                        Indentation = builder.Indentation + 4,
                        ParseState = ParseState.InOrderedList,
                        OrderedListItemNumber = 0,
                    }),
            "ul" => new MrkdwnTag(
                startDelimiter: "",
                endDelimiter: "\n",
                builder with
                {
                    ParseState = ParseState.InUnorderedList,
                    OrderedListItemNumber = 0,
                }),
            "ol" => new MrkdwnTag(
                startDelimiter: "",
                endDelimiter: "\n",
                builder with
                {
                    ParseState = ParseState.InOrderedList,
                    OrderedListItemNumber = 0,
                }),
            "li" when builder.ParseState is ParseState.InUnorderedList
                => new MrkdwnTag(
                    startDelimiter: $"\n{Indent()}* ",
                    endDelimiter: "",
                    builder),
            "li" when builder.ParseState is ParseState.InOrderedList
                => new MrkdwnTag(
                    startDelimiter: $"\n{Indent()}{++builder.OrderedListItemNumber}. ",
                    endDelimiter: "",
                    builder),
            "h1" or "h2" or "h3" or "h4" => new MrkdwnTag("\n*", "*\n", builder),
            "div" when node.Attributes["class"]?.Value is "zd-indent"
                => new MrkdwnTag("", "", builder.Indent(node)),
            _ => null
        };
    }

    MrkdwnTag(string startDelimiter, MrkdwnBuilder builder) : this(startDelimiter, startDelimiter, builder)
    {
    }

    MrkdwnTag(string startDelimiter, string endDelimiter, MrkdwnBuilder builder)
    {
        Builder = builder;
        Builder.Append(startDelimiter);
        _endDelimiter = endDelimiter;
    }

    public void Dispose()
    {
        Builder.Append(_endDelimiter);
    }
}
