using System.Linq;
using Ganss.XSS;
using Markdig;
using Markdig.Extensions.MediaLinks;
using Markdig.Syntax;

namespace Serious.Razor.Components.Services;

public class MarkdownService : IMarkdownService
{
    readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = BuildPipeline();
    }

    public string Render(string markdownContent)
    {
        var result = Markdown.ToHtml(markdownContent, _pipeline);

        // Sanitize the HTML to protect from jerks.
        return SanitizeHtml(result);
    }

    public static string SanitizeHtml(string markdownContent)
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Add("blockquote");
        sanitizer.AllowedTags.Add("footer");
        sanitizer.AllowedTags.Add("video");
        sanitizer.AllowedTags.Add("source");
        sanitizer.AllowedTags.Add("iframe");
        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedAttributes.Add("controls");

        return sanitizer.Sanitize(markdownContent);
    }

    public MarkdownDocument Parse(string markdownContent)
    {
        return Markdown.Parse(markdownContent, _pipeline);
    }

    static MarkdownPipeline BuildPipeline()
    {
        var pipelineBuilder = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .UseMediaLinks();

        // QuickTime / .mov was not supported out of the box. Treat .mov uploads as video.
        // The approach for this was taken from https://markheath.net/post/markdown-html-yaml-front-matter
        var mov = pipelineBuilder.Extensions.OfType<MediaLinkExtension>().Single();
        mov.Options.ExtensionToMimeType[".mov"] = "video/mp4";

        return pipelineBuilder.Build();
    }
}
