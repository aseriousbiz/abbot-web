using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Serious.Razor.TagHelpers;

namespace Serious.Razor.Components.Models;

/// <summary>
/// Model supplied to the _MarkdownEditor partial via the <see cref="MarkdownEditorTagHelper" />.
/// </summary>
public class MarkdownEditorModel
{
    /// <summary>
    /// Creates a <see cref="MarkdownEditorModel"/>.
    /// </summary>
    /// <param name="htmlHelper"></param>
    /// <param name="modelExpression">Model expression for the underlying textarea</param>
    /// <param name="placeholder">Placeholder text when the textarea is empty</param>
    public MarkdownEditorModel(IHtmlHelper htmlHelper, ModelExpression modelExpression, string placeholder)
    {
        Html = htmlHelper;
        For = modelExpression;
        Placeholder = placeholder;
    }

    /// <summary>
    /// The model expression for the markdown editor specified by the asp-for attribute in the tag helper.
    /// This is passed to the textarea.
    /// </summary>
    public ModelExpression For { get; }

    /// <summary>
    /// The placeholder text to display when the markdown editor has no content.
    /// </summary>
    public string Placeholder { get; }

    /// <summary>
    /// Access to the HtmlHelper from the source.
    /// </summary>
    public IHtmlHelper Html { get; }
}
