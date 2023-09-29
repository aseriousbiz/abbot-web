using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.TagHelpers;
using Serious.Abbot.Messaging;
using Serious.Slack;

public class MessageRendererTagHelperTests
{
    public class TheProcessMethod
    {
        [Fact]
        public void RendersInvalidTextFormat()
        {
            var tagHelper = new MessageRendererTagHelper
            {
                Message = new RenderedMessage(new RenderedMessageSpan[]
                {
                    new FormattedTextSpan("*world!*", new[] { new PlainTextSpan("world!") }, (TextFormat)int.MaxValue)
                })
            };
            var tagHelperContext = new TagHelperContext(
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                Guid.NewGuid().ToString("N"));
            var tagHelperOutput = new TagHelperOutput(
                "div",
                new TagHelperAttributeList(),
                (_, _) => {
                    var tagHelperContent = new DefaultTagHelperContent()
                        .SetHtmlContent(new HtmlString(""));
                    return Task.FromResult(tagHelperContent);
                });

            tagHelper.Process(tagHelperContext, tagHelperOutput);

            Assert.Equal("div", tagHelperOutput.TagName);
            Assert.Equal(
                """<code class="text-red-500">An error occurred while rendering this message. Abbot Support has been notified and is working on the problem!</code>""",
                tagHelperOutput.Content.GetContent());
        }

        [Fact]
        public void RendersHtmlFromMkdwn()
        {
            var tagHelper = new MessageRendererTagHelper
            {
                Message = new RenderedMessage(new RenderedMessageSpan[]
                {
                    new EmojiSpan(":sparkles:", new UnicodeEmoji("sparkles", "&#x2728;")),
                    new PlainTextSpan(" Hello, "),
                    new FormattedTextSpan("*world!*", new[] { new PlainTextSpan("world!") }, TextFormat.Bold),
                    new PlainTextSpan(" "),
                    new UserMentionSpan("<@U0123456>", "U0123456", new Member { User = new() { DisplayName = "Jane Doe" } }),
                    new PlainTextSpan(" in room "),
                    new RoomMentionSpan("<#C0123456>", "C0123456", new Room { Name = "General" }),
                })
            };
            var tagHelperContext = new TagHelperContext(
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                Guid.NewGuid().ToString("N"));
            var tagHelperOutput = new TagHelperOutput(
                "div",
                new TagHelperAttributeList(),
                (_, _) => {
                    var tagHelperContent = new DefaultTagHelperContent()
                        .SetHtmlContent(new HtmlString(""));
                    return Task.FromResult(tagHelperContent);
                });

            tagHelper.Process(tagHelperContext, tagHelperOutput);

            Assert.Equal("div", tagHelperOutput.TagName);
            Assert.Equal(
                @"&#x2728; Hello, <strong>world!</strong> <span class=""msg msg-mention"">@Jane Doe</span> in room <span class=""msg msg-mention"">#General</span>",
                tagHelperOutput.Content.GetContent());
        }

        [Fact]
        public void RendersItalicInsideBold()
        {
            var tagHelper = new MessageRendererTagHelper
            {
                Message = new RenderedMessage(new RenderedMessageSpan[]
                {
                    new FormattedTextSpan(
                        "*Hey, _this italic_ is in bold!*",
                        new RenderedMessageSpan[]
                        {
                            new PlainTextSpan("Hey, "),
                            new FormattedTextSpan(
                                "_this italic_",
                                new[]
                                {
                                    new PlainTextSpan("this italic")
                                },
                                Format: TextFormat.Italic),
                            new PlainTextSpan(" is in bold!")
                        },
                        Format: TextFormat.Bold),
                })
            };
            var tagHelperContext = new TagHelperContext(
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                Guid.NewGuid().ToString("N"));
            var tagHelperOutput = new TagHelperOutput(
                "div",
                new TagHelperAttributeList(),
                (_, _) => {
                    var tagHelperContent = new DefaultTagHelperContent()
                        .SetHtmlContent(new HtmlString(""));
                    return Task.FromResult(tagHelperContent);
                });

            tagHelper.Process(tagHelperContext, tagHelperOutput);

            Assert.Equal("div", tagHelperOutput.TagName);
            Assert.Equal(
                @"<strong>Hey, <em>this italic</em> is in bold!</strong>",
                tagHelperOutput.Content.GetContent());
        }

        [Theory]
        [InlineData("_This is italic!_", "<em>This is italic!</em>")]
        [InlineData("*Hey, this is bold!*", "<strong>Hey, this is bold!</strong>")]
        [InlineData("Hey, *this bold text _has italics in_ it!*", "Hey, <strong>this bold text <em>has italics in</em> it!</strong>")]
        public async Task RendersEndToEndMrkdwnToHtml(string mrkdwn, string expectedHtml)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var renderer = env.Activate<MessageRenderer>();
            var renderedMessage = await renderer.RenderMessageAsync(mrkdwn, organization);
            var tagHelper = new MessageRendererTagHelper
            {
                Message = renderedMessage
            };
            var tagHelperContext = new TagHelperContext(
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                Guid.NewGuid().ToString("N"));
            var tagHelperOutput = new TagHelperOutput(
                "div",
                new TagHelperAttributeList(),
                (_, _) => {
                    var tagHelperContent = new DefaultTagHelperContent()
                        .SetHtmlContent(new HtmlString(""));
                    return Task.FromResult(tagHelperContent);
                });

            tagHelper.Process(tagHelperContext, tagHelperOutput);

            Assert.Equal("div", tagHelperOutput.TagName);
            Assert.Equal(expectedHtml, tagHelperOutput.Content.GetContent());
        }

        [Fact]
        public void RendersNothingForEmptyRenderedMessage()
        {
            var tagHelper = new MessageRendererTagHelper
            {
                Message = new RenderedMessage(Array.Empty<RenderedMessageSpan>())
            };
            var tagHelperContext = new TagHelperContext(
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                Guid.NewGuid().ToString("N"));
            var tagHelperOutput = new TagHelperOutput(
                "div",
                new TagHelperAttributeList(),
                (_, _) => {
                    var tagHelperContent = new DefaultTagHelperContent()
                        .SetHtmlContent(new HtmlString(""));
                    return Task.FromResult(tagHelperContent);
                });

            tagHelper.Process(tagHelperContext, tagHelperOutput);

            Assert.Null(tagHelperOutput.TagName);
            Assert.Equal("", tagHelperOutput.Content.GetContent());
        }

        [Fact]
        public void RendersNothingForNullMessage()
        {
            var tagHelper = new MessageRendererTagHelper();
            var tagHelperContext = new TagHelperContext(
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                Guid.NewGuid().ToString("N"));
            var tagHelperOutput = new TagHelperOutput(
                "div",
                new TagHelperAttributeList(),
                (_, _) => {
                    var tagHelperContent = new DefaultTagHelperContent()
                        .SetHtmlContent(new HtmlString(""));
                    return Task.FromResult(tagHelperContent);
                });

            tagHelper.Process(tagHelperContext, tagHelperOutput);

            Assert.Null(tagHelperOutput.TagName);
            Assert.Equal("", tagHelperOutput.Content.GetContent());
        }

        [Theory]
        [InlineData(null, @"&#x2728; Hello, world! <span class=""msg msg-mention"">@Jane Doe</span> in room <span class=""msg msg-mention"">#General</span>")]
        [InlineData(7, @"&#x2728; Hello,&#x2026;")]
        [InlineData(18, @"&#x2728; Hello, world! <span class=""msg msg-mention"">@Jane Doe</span>")] // We never truncate in the middle of a mention.
        [InlineData(4, @"&#x2728; Hello,&#x2026;")] // Never truncate in the middle of a word.
        [InlineData(10, @"&#x2728; Hello, world!&#x2026;")] // Never truncate in the middle of a word.
        [InlineData(28, @"&#x2728; Hello, world! <span class=""msg msg-mention"">@Jane Doe</span> in&#x2026;")]
        [InlineData(35, @"&#x2728; Hello, world! <span class=""msg msg-mention"">@Jane Doe</span> in room <span class=""msg msg-mention"">#General</span>")]
        public void TruncatesTextButNotInMiddleOfMention(int? truncateLength, string expected)
        {
            var tagHelper = new MessageRendererTagHelper
            {
                TruncateLength = truncateLength,
                Message = new RenderedMessage(new RenderedMessageSpan[]
                {
                    new EmojiSpan(":sparkles:", new UnicodeEmoji("sparkles", "&#x2728;")),
                    new PlainTextSpan(" Hello, world! "),
                    new UserMentionSpan("<@U0123456>", "U0123456", new Member { User = new() { DisplayName = "Jane Doe" } }),
                    new PlainTextSpan(" in room "),
                    new RoomMentionSpan("<#C0123456>", "C0123456", new Room { Name = "General" }),
                }),
                RenderLinks = true,
            };
            var tagHelperContext = new TagHelperContext(
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                Guid.NewGuid().ToString("N"));
            var tagHelperOutput = new TagHelperOutput(
                "div",
                new TagHelperAttributeList(),
                (_, _) => {
                    var tagHelperContent = new DefaultTagHelperContent()
                        .SetHtmlContent(new HtmlString(""));
                    return Task.FromResult(tagHelperContent);
                });

            tagHelper.Process(tagHelperContext, tagHelperOutput);

            Assert.Equal("div", tagHelperOutput.TagName);
            Assert.Equal(expected, tagHelperOutput.Content.GetContent());
        }

        [Theory]
        [InlineData(2, "Hey&#x2026;")]
        [InlineData(7, "Hey there&#x2026;")]
        [InlineData(26, "Hey there this is some words")]
        [InlineData(28, "Hey there this is some words")]
        public void TruncatesLinkContentOnWordBoundaryButNotHref(int truncateLength, string expectedLinkText)
        {
            var tagHelper = new MessageRendererTagHelper
            {
                TruncateLength = truncateLength,
                Message = new RenderedMessage(new RenderedMessageSpan[]
                {
                    new LinkSpan(
                        "Hey there this is some words",
                        "https://example.com/foo/bar/baz/biz",
                        new [] { new PlainTextSpan("Hey there this is some words") }),
                }),
                RenderLinks = true,
            };
            var tagHelperContext = new TagHelperContext(
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                Guid.NewGuid().ToString("N"));
            var tagHelperOutput = new TagHelperOutput(
                "div",
                new TagHelperAttributeList(),
                (_, _) => {
                    var tagHelperContent = new DefaultTagHelperContent()
                        .SetHtmlContent(new HtmlString(""));
                    return Task.FromResult(tagHelperContent);
                });

            tagHelper.Process(tagHelperContext, tagHelperOutput);

            Assert.Equal("div", tagHelperOutput.TagName);
            Assert.Equal($"<a href=\"https://example.com/foo/bar/baz/biz\">{expectedLinkText}</a>", tagHelperOutput.Content.GetContent());
        }

        [Theory]
        [InlineData(2, "https://example.com/foo/bar/baz/biz/foo/bar/baz/biz/boo", "https://example.com/foo/bar/baz/&#x2026;")]
        [InlineData(64, "https://example.com/foo/bar/baz/biz/foo/bar/baz/biz/boo", "https://example.com/foo/bar/baz/biz/foo/bar/baz/biz/boo")]
        [InlineData(7, "hey, this-is-just-a-really-long-word-that-is-not-a-url-but-kind-of-ridiculous-do-you-not-think?", "hey, this-is-just-a-really-long-&#x2026;")]
        [InlineData(124, "hey, this-is-just-a-really-long-word-that-is-not-a-url-but-kind-of-ridiculous-do-you-not-think?", "hey, this-is-just-a-really-long-word-that-is-not-a-url-but-kind-of-ridiculous-do-you-not-think?")]
        public void TruncatesReallyLongLinkContentButNotHref(int truncateLength, string linkText, string expectedLinkText)
        {
            var tagHelper = new MessageRendererTagHelper
            {
                TruncateLength = truncateLength,
                Message = new RenderedMessage(new RenderedMessageSpan[]
                {
                    new LinkSpan(
                        linkText,
                        "https://example.com/foo/bar/baz/biz/foo/bar/baz/biz/boo",
                        new [] { new PlainTextSpan(linkText) }),
                }),
                RenderLinks = true,
            };
            var tagHelperContext = new TagHelperContext(
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                Guid.NewGuid().ToString("N"));
            var tagHelperOutput = new TagHelperOutput(
                "div",
                new TagHelperAttributeList(),
                (_, _) => {
                    var tagHelperContent = new DefaultTagHelperContent()
                        .SetHtmlContent(new HtmlString(""));
                    return Task.FromResult(tagHelperContent);
                });

            tagHelper.Process(tagHelperContext, tagHelperOutput);

            Assert.Equal("div", tagHelperOutput.TagName);
            Assert.Equal($"<a href=\"https://example.com/foo/bar/baz/biz/foo/bar/baz/biz/boo\">{expectedLinkText}</a>", tagHelperOutput.Content.GetContent());
        }

        [Fact]
        public void DoesNotRenderLinksByDefault()
        {
            var tagHelper = new MessageRendererTagHelper
            {
                Message = new RenderedMessage(new RenderedMessageSpan[]
                {
                    new LinkSpan(
                        "Some link text",
                        "https://example.com/foo/bar/baz/biz/foo/bar/baz/biz/boo",
                        new [] { new PlainTextSpan("Some link text") }),
                })
            };
            var tagHelperContext = new TagHelperContext(
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                Guid.NewGuid().ToString("N"));
            var tagHelperOutput = new TagHelperOutput(
                "div",
                new TagHelperAttributeList(),
                (_, _) => {
                    var tagHelperContent = new DefaultTagHelperContent()
                        .SetHtmlContent(new HtmlString(""));
                    return Task.FromResult(tagHelperContent);
                });

            tagHelper.Process(tagHelperContext, tagHelperOutput);

            Assert.Equal("div", tagHelperOutput.TagName);
            Assert.Equal($"Some link text", tagHelperOutput.Content.GetContent());
        }

        [Fact]
        public void WhenRenderNewlinesTrueRendersBrTags()
        {
            var tagHelper = new MessageRendererTagHelper
            {
                Message = new RenderedMessage(new RenderedMessageSpan[]
                {
                    new PlainTextSpan("Line1\nLine2 "),
                    new LinkSpan(
                        "Line3\nLine4",
                        "https://example.com/foo/bar/baz/biz/foo/bar/baz/biz/boo",
                        new [] { new PlainTextSpan("Line3\nLine4") }),
                }),
                RenderNewlines = true
            };
            var tagHelperContext = new TagHelperContext(
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                Guid.NewGuid().ToString("N"));
            var tagHelperOutput = new TagHelperOutput(
                "div",
                new TagHelperAttributeList(),
                (_, _) => {
                    var tagHelperContent = new DefaultTagHelperContent()
                        .SetHtmlContent(new HtmlString(""));
                    return Task.FromResult(tagHelperContent);
                });

            tagHelper.Process(tagHelperContext, tagHelperOutput);

            Assert.Equal("div", tagHelperOutput.TagName);
            Assert.Equal("Line1<br />Line2 Line3<br />Line4", tagHelperOutput.Content.GetContent());
        }

        [Fact]
        public void TruncatesReallyLongTextContent()
        {
            var tagHelper = new MessageRendererTagHelper
            {
                TruncateLength = 10,
                Message = new RenderedMessage(new RenderedMessageSpan[]
                {
                    new PlainTextSpan("hey, this-is-just-a-really-long-word-that-is-not-a-url-but-kind-of-ridiculous-do-you-not-think?"),
                }),
                RenderNewlines = true,
            };
            var tagHelperContext = new TagHelperContext(
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                Guid.NewGuid().ToString("N"));
            var tagHelperOutput = new TagHelperOutput(
                "div",
                new TagHelperAttributeList(),
                (_, _) => {
                    var tagHelperContent = new DefaultTagHelperContent()
                        .SetHtmlContent(new HtmlString(""));
                    return Task.FromResult(tagHelperContent);
                });

            tagHelper.Process(tagHelperContext, tagHelperOutput);

            Assert.Equal("div", tagHelperOutput.TagName);
            Assert.Equal("hey, this-is-just-a-really-long-&#x2026;", tagHelperOutput.Content.GetContent());
        }
    }
}
