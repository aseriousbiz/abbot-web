using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.TagHelpers;

public enum PillSize
{
    Small,
    Medium,
}

public enum PillColor
{
    Green,
    Red,
    Yellow,
    Gray,
    Blue,
    Indigo,
    Slate,
}

[HtmlTargetElement("pill", TagStructure = TagStructure.NormalOrSelfClosing)]
public class PillTagHelper : TagHelper
{
    static readonly ILogger<PillTagHelper> Log = ApplicationLoggerFactory.CreateLogger<PillTagHelper>();

    // We have a static lookup for Bg and Text colors because then tailwind can tree-shake unused colors
    static readonly Dictionary<PillColor, string> BgColors = new()
    {
        { PillColor.Green, "bg-green-100" },
        { PillColor.Red, "bg-red-100" },
        { PillColor.Yellow, "bg-yellow-100" },
        { PillColor.Gray, "bg-gray-100" },
        { PillColor.Slate, "bg-slate-100" },
        { PillColor.Blue, "bg-blue-100" },
        { PillColor.Indigo, "bg-indigo-100" },
    };

    static readonly Dictionary<PillColor, string> TextColors = new()
    {
        { PillColor.Green, "text-green-600" },
        { PillColor.Red, "text-red-600" },
        { PillColor.Yellow, "text-yellow-600" },
        { PillColor.Gray, "text-gray-600" },
        { PillColor.Slate, "text-slate-600" },
        { PillColor.Blue, "text-blue-600" },
        { PillColor.Indigo, "text-indigo-600" },
    };

    /// <summary>
    /// Gets or sets the color for the pill.
    /// </summary>
    public PillColor Color { get; set; }

    /// <summary>
    /// Gets or sets the name of a Font Awesome icon (including the 'fa-' prefix) to include in the pill.
    /// May include modifiers like 'fa-spin-pulse'.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets a tooltip to display when the user hovers over the pill.
    /// </summary>
    public string? Tooltip { get; set; }

    /// <summary>
    /// Gets or sets the PillSize and sets styles accordingly
    /// </summary>
    public PillSize Size { get; set; } = PillSize.Medium;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;

        var bgColorClass = GetClass("Background", BgColors);
        var textColorClass = GetClass("Text", TextColors);

        var iconSize = Size is PillSize.Small
            ? "13px"
            : "16px";

        var sizeClasses = Size is PillSize.Small
            ? "px-1.5 py-0.5 text-xs gap-x-0.5"
            : "px-2 py-1 gap-x-1";

        output.Attributes.SetAttribute("class",
            $"inline-flex {sizeClasses} {bgColorClass} {textColorClass} rounded font-medium items-center");

        if (Tooltip is { Length: > 0 })
        {
            output.Attributes.SetAttribute("data-tooltip", Tooltip);
            output.Attributes.SetAttribute("title", Tooltip);
        }

        if (Icon is { Length: > 0 })
        {
            output.PreContent.AppendHtml(
                $"""<i class="mr-1 fa {Icon}" style="height: {iconSize}; width: {iconSize}" aria-hidden></i>""");
        }
    }

    string GetClass(string colorType, Dictionary<PillColor, string> lookup)
    {
        string bgColorClass;
        if (lookup.TryGetValue(Color, out var pillColor))
        {
            bgColorClass = pillColor;
        }
        else
        {
            Log.NoColorMapping(colorType, Color);
            return lookup[PillColor.Gray];
        }

        return bgColorClass;
    }
}

public static partial class PillTagHelperLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Critical, // This indicates a programming error, not a runtime error
        Message = "No {ColorType} mapping for pill color {Color}.")]
    public static partial void NoColorMapping(this ILogger<PillTagHelper> logger, string colorType, PillColor color);
}
