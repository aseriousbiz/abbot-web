using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.FeatureManagement;
using Serious.Abbot.FeatureManagement;

namespace Serious.Abbot.Infrastructure.TagHelpers;

// Mostly copied from https://github.com/microsoft/FeatureManagement-Dotnet/blob/main/src/Microsoft.FeatureManagement.AspNetCore/TagHelpers/FeatureTagHelper.cs
// But using explicit feature context stored in the HttpContext.

[HtmlTargetElement(Attributes = "feature")]
public class FeatureAttributeTagHelper : FeatureTagHelper
{
    public FeatureAttributeTagHelper(FeatureService featureService) : base(featureService)
    {
    }

    [HtmlAttributeName("feature")]
    public override string? Name { get; set; }

    [HtmlAttributeName("feature-requirement")]
    public override RequirementType Requirement { get; set; }

    [HtmlAttributeName("feature-negate")]
    public override bool Negate { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // Keep output.TagName
        await SuppressUnlessEnabled(output).ConfigureAwait(false);
    }
}

[HtmlTargetElement("feature")]
public class FeatureTagHelper : TagHelper
{
    readonly FeatureService _featureService;

    /// <summary>
    /// A feature name, or comma separated list of feature names, for which the content should be rendered. By default, all specified features must be enabled to render the content, but this requirement can be controlled by the <see cref="Requirement"/> property.
    /// </summary>
    public virtual string? Name { get; set; }

    /// <summary>
    /// Controls whether 'All' or 'Any' feature in a list of features should be enabled to render the content within the feature tag.
    /// </summary>
    public virtual RequirementType Requirement { get; set; } = RequirementType.All;

    /// <summary>
    /// Negates the evaluation for whether or not a feature tag should display content. This is used to display alternate content when a feature or set of features are disabled.
    /// </summary>
    public virtual bool Negate { get; set; }

    // MVC will inject a ViewContext here.
    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    /// <summary>
    /// Creates a feature tag helper.
    /// </summary>
    /// <param name="featureService">The <see cref="FeatureService"/> to use to evaluate feature state.</param>
    public FeatureTagHelper(FeatureService featureService)
    {
        _featureService = featureService;
    }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        await SuppressUnlessEnabled(output).ConfigureAwait(false);
    }

    protected async Task SuppressUnlessEnabled(TagHelperOutput output)
    {
        // Get the targeting context
        var actor = ViewContext.HttpContext.GetFeatureActor();

        bool enabled = false;

        if (!string.IsNullOrEmpty(Name))
        {
            var names = Name.Split(',').Select(n => n.Trim());

            enabled = await _featureService.IsEnabledAsync(names, actor, Requirement).ConfigureAwait(false);
        }

        if (Negate)
        {
            enabled = !enabled;
        }

        if (!enabled)
        {
            output.SuppressOutput();
        }
    }
}
