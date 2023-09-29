using System;
using System.Collections.Generic;
using System.Text;

namespace Serious.Abbot.Integrations.Zendesk;

public class HtmlBuilder
{
    const string IndentationText = "    ";
    const string NewLineText = "\n";

    readonly StringBuilder _stringBuilder;
    readonly int _indentation;

    public HtmlBuilder() : this(HtmlFormatting.None)
    {
    }

    public HtmlBuilder(HtmlFormatting formatting) : this(new StringBuilder(), formatting, 0)
    {
    }

    public HtmlBuilder(StringBuilder stringBuilder, HtmlFormatting formatting, int initialIndentation)
    {
        _stringBuilder = stringBuilder;
        Formatting = formatting;
        _indentation = initialIndentation;
    }

    public HtmlFormatting Formatting { get; }

    public ElementWriter AppendTag(string tagName) => AppendTag(tagName, insideInlineTagContent: false);

    public ElementWriter AppendTag(string tagName, bool insideInlineTagContent)
    {
        return ElementWriter.WriteTag(
            tagName,
            attributes: null,
            htmlBuilder: this,
            Formatting,
            insideInlineTagContent);
    }

    public ElementWriter AppendTag(
        string tagName,
        IReadOnlyDictionary<string, string> attributes,
        bool insideInlineTagContent)
    {
        return ElementWriter.WriteTag(
            tagName,
            attributes,
            htmlBuilder: this,
            Formatting,
            insideInlineTagContent);
    }

    public ElementWriter AppendTag(
        string tagName,
        string? style) =>
            AppendTag(tagName, style, insideInlineTagContent: false);

    public ElementWriter AppendTag(string tagName, string? style, bool insideInlineTagContent)
    {
        return style is null
            ? AppendTag(tagName, insideInlineTagContent)
            : AppendTag(
                tagName,
                new Dictionary<string, string>
                {
                    ["style"] = style
                },
                insideInlineTagContent);
    }

    public HtmlBuilder Indent() => new(_stringBuilder, Formatting, _indentation + 1);

    public void Append(string text) => _stringBuilder.Append(text);

    public override string ToString() => _stringBuilder.ToString();

    public void AppendLine() => Append(NewLineText);

    public void AppendIndentation()
    {
        for (int i = 0; i < _indentation; i++)
        {
            Append(IndentationText);
        }
    }
}

public class ElementWriter : IDisposable
{
    readonly HtmlBuilder _builder;
    readonly HtmlFormatting _formatting;
    readonly bool _inlineContainer;
    readonly bool _insideInlineTagContent;
    readonly string _tagName;
    readonly IReadOnlyDictionary<string, string>? _attributes;

    ElementWriter(
        string tagName,
        IReadOnlyDictionary<string, string>? attributes,
        HtmlBuilder builder,
        HtmlFormatting formatting,
        bool inlineContainer,
        bool insideInlineTagContent)
    {
        _builder = builder;
        _formatting = formatting;
        _inlineContainer = inlineContainer;
        _insideInlineTagContent = insideInlineTagContent;
        _tagName = tagName;
        _attributes = attributes;
    }

    public static ElementWriter WriteTag(
        string tagName,
        IReadOnlyDictionary<string, string>? attributes,
        HtmlBuilder htmlBuilder,
        HtmlFormatting formatting,
        bool insideInlineTagContent)
    {
        var inlineContainer = tagName is "li" or "span" or "a" or "code";
        var elementWriter = new ElementWriter(
            tagName,
            attributes,
            htmlBuilder,
            formatting,
            inlineContainer,
            insideInlineTagContent);
        elementWriter.AppendBeginTag();
        return elementWriter;
    }

    void AppendBeginTag()
    {
        if (_formatting is HtmlFormatting.Indented && !_insideInlineTagContent)
        {
            _builder.AppendIndentation();
        }
        _builder.Append($"<{_tagName}");
        if (_attributes is { Count: > 0 })
        {
            foreach (var attribute in _attributes)
            {
                _builder.Append($" {attribute.Key}=\"{attribute.Value}\"");
            }
        }

        _builder.Append(">");
        if (_formatting is HtmlFormatting.Indented && !_insideInlineTagContent && !_inlineContainer)
        {
            _builder.AppendLine();
        }
    }

    void AppendEndTag()
    {
        // Some tag elements should be rendered inline. We'll hard-code that list for now.
        if (_formatting is HtmlFormatting.Indented && !_insideInlineTagContent && !_inlineContainer)
        {
            _builder.AppendIndentation();
        }
        _builder.Append($"</{_tagName}>");
        if (_formatting is HtmlFormatting.Indented && !_insideInlineTagContent)
        {
            _builder.AppendLine();
        }
    }

    public void Dispose()
    {
        AppendEndTag();
    }
}

/// <summary>
/// Controls how HTML is formatted.
/// </summary>
public enum HtmlFormatting
{
    /// <summary>
    /// No special formatting applied.
    /// </summary>
    None,

    /// <summary>
    /// HTML is rendered in a human-readable indented style.
    /// </summary>
    Indented,
}
