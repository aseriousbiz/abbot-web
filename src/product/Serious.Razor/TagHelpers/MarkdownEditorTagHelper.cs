using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Serious.Razor.Components.Models;

namespace Serious.Razor.TagHelpers;

[HtmlTargetElement("markdowneditor")]
public class MarkdownEditorTagHelper : TagHelper
{
    const string ForAttributeName = "asp-for";

    readonly IServiceProvider _serviceProvider;
    readonly HtmlEncoder _htmlEncoder;

    public MarkdownEditorTagHelper(IServiceProvider serviceProvider, HtmlEncoder htmlEncoder)
    {
        _serviceProvider = serviceProvider;
        _htmlEncoder = htmlEncoder;
    }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; } = null!;

    /// <summary>
    /// Placeholder text displayed if the textarea has no content.
    /// </summary>
    public string? Placeholder { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        var originExplorer = For.ModelExplorer;
        var newModel = originExplorer.Model;
        var newExplorer = originExplorer.GetExplorerForModel(newModel);
        var newFor = new ModelExpression(For.Name, newExplorer);
        var modelType = originExplorer.Container.Model.GetType();

        var htmlHelperType = typeof(IHtmlHelper<>).MakeGenericType(modelType);
        var htmlHelper = _serviceProvider.GetService(htmlHelperType) as IHtmlHelper;   // get the actual IHtmlHelper<TModel>
        if (htmlHelper is null)
        {
            throw new InvalidOperationException($"Could not retrieve an IHtmlHelper of type {htmlHelperType.Name}.");
        }
        (htmlHelper as IViewContextAware)?.Contextualize(ViewContext);

        var model = new MarkdownEditorModel(htmlHelper, newFor, Placeholder ?? string.Empty);
        var partialView = await htmlHelper.PartialAsync("_MarkdownEditor", model);

        using var writer = new StringWriter();
        partialView.WriteTo(writer, _htmlEncoder);
        output.Content.SetHtmlContent(writer.ToString());
    }
}
