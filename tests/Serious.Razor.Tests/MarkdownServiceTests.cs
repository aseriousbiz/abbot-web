using Serious.Razor.Components.Services;
using Xunit;

public class MarkdownServiceTests
{
    public class TheSanitizeHtmlMethod
    {
        [Fact]
        public void SanitizesHtml()
        {
            const string html = "<img src onerror=alert('xss')>";

            var sanitized = MarkdownService.SanitizeHtml(html);

            Assert.Equal("<img src=\"\">", sanitized);
        }
    }

}
