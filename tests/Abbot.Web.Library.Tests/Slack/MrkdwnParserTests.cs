using Serious.Abbot.Messaging;
using Serious.Slack.Parsing;

public class MrkdwnParserTests
{
    [Fact]
    public void FullParseTest()
    {
        const string message = @":sparkles: <@U1234> can you add <@U5678|steve> to <#C1234|my channel>? Also add them to <!subteam^1234>. I guess anyone <!here> could do that though. Just go to <https://www.google.com|Google> or <https://www.bing.com> and search it up.";
        var parsed = MrkdwnParser.Parse(message);
        Assert.Equal(new MrkdwnSpan[]
        {
            new MrkdwnEmoji(":sparkles:", "sparkles"),
            new MrkdwnPlainText(" "),
            new MrkdwnMention("<@U1234>", SlackMentionType.User, "U1234", null),
            new MrkdwnPlainText(" can you add "),
            new MrkdwnMention("<@U5678|steve>", SlackMentionType.User, "U5678", "steve"),
            new MrkdwnPlainText(" to "),
            new MrkdwnMention("<#C1234|my channel>", SlackMentionType.Channel, "C1234", "my channel"),
            new MrkdwnPlainText("? Also add them to "),
            new MrkdwnMention("<!subteam^1234>", SlackMentionType.UserGroup, "1234", null),
            new MrkdwnPlainText(". I guess anyone "),
            new MrkdwnMention("<!here>", SlackMentionType.AtHere, null, null),
            new MrkdwnPlainText(" could do that though. Just go to "),
            new MrkdwnLink("<https://www.google.com|Google>", "https://www.google.com", new [] { new MrkdwnPlainText("Google") }),
            new MrkdwnPlainText(" or "),
            new MrkdwnLink("<https://www.bing.com>", "https://www.bing.com", new [] { new MrkdwnPlainText("https://www.bing.com") }),
            new MrkdwnPlainText(" and search it up."),
        }, parsed.Spans.ToArray());
    }

    [Theory]
    [InlineData("<@U1234>", SlackMentionType.User, "U1234", null)]
    [InlineData("<@U1234|label>", SlackMentionType.User, "U1234", "label")]
    [InlineData("<@W1234>", SlackMentionType.User, "W1234", null)]
    [InlineData("<@W1234|label>", SlackMentionType.User, "W1234", "label")]
    [InlineData("<#C1234>", SlackMentionType.Channel, "C1234", null)]
    [InlineData("<#C1234|label>", SlackMentionType.Channel, "C1234", "label")]
    [InlineData("<!subteam^1234>", SlackMentionType.UserGroup, "1234", null)]
    [InlineData("<!subteam^1234|label>", SlackMentionType.UserGroup, "1234", "label")]
    [InlineData("<!here>", SlackMentionType.AtHere, null, null)]
    [InlineData("<!channel>", SlackMentionType.AtChannel, null, null)]
    [InlineData("<!everyone>", SlackMentionType.AtEveryone, null, null)]
    public void CanParseMentions(string text, SlackMentionType expectedType, string? expectedId, string? expectedLabel)
    {
        var parsed = MrkdwnParser.Parse(text);
        Assert.Equal(new MrkdwnMention(text, expectedType, expectedId, expectedLabel), parsed.Spans.Single());
    }

    [Theory]
    [InlineData("<https://www.google.com|Google>", "https://www.google.com", "Google")]
    [InlineData("<https://www.bing.com>", "https://www.bing.com", "https://www.bing.com")]
    public void CanParseLinks(string text, string url, string label)
    {
        var parsed = MrkdwnParser.Parse(text);
        var mrkdwnLink = Assert.IsType<MrkdwnLink>(Assert.Single(parsed.Spans));
        Assert.Equal(url, mrkdwnLink.Url);
        var linkText = Assert.IsType<MrkdwnPlainText>(Assert.Single(mrkdwnLink.FormattedSpans)).Text;
        Assert.Equal(label, linkText);
    }

    [Fact]
    public void CanParseLinksWithNestedFormattedText()
    {
        var parsed = MrkdwnParser.Parse("<https://www.google.com|this is *bold* text>");

        var mrkdwnLink = Assert.IsType<MrkdwnLink>(Assert.Single(parsed.Spans));
        Assert.Equal("<https://www.google.com|this is *bold* text>", mrkdwnLink.OriginalText);
        Assert.Collection(mrkdwnLink.FormattedSpans,
            i => Assert.Equal("this is ", Assert.IsType<MrkdwnPlainText>(i).Text),
            i => {
                var nestedSpan = Assert.IsType<MrkdwnFormattedSpans>(i);
                Assert.Equal("*bold*", nestedSpan.OriginalText);
            },
            i => Assert.Equal(" text", Assert.IsType<MrkdwnPlainText>(i).Text));
    }

    [Fact]
    public void CanParseBoldText()
    {
        var parsed = MrkdwnParser.Parse("Hey *this is bold* text");
        Assert.Collection(parsed.Spans,
            s => Assert.Equal("Hey ", Assert.IsType<MrkdwnPlainText>(s).Text),
            s => {
                var span = Assert.IsType<MrkdwnFormattedSpans>(s);
                Assert.Equal(TextFormat.Bold, span.Format);
                var formattedSpan = Assert.IsType<MrkdwnPlainText>(Assert.Single(span.FormattedSpans));
                Assert.Equal("this is bold", formattedSpan.Text);
                Assert.Equal("*this is bold*", span.OriginalText);
            },
            s => Assert.Equal(" text", Assert.IsType<MrkdwnPlainText>(s).Text));
    }

    [Fact]
    public void CanParseItalicText()
    {
        var parsed = MrkdwnParser.Parse("Hey _this is italic_ text");
        Assert.Collection(parsed.Spans,
            s => Assert.Equal("Hey ", Assert.IsType<MrkdwnPlainText>(s).Text),
            s => {
                var span = Assert.IsType<MrkdwnFormattedSpans>(s);
                Assert.Equal(TextFormat.Italic, span.Format);
                var formattedSpan = Assert.IsType<MrkdwnPlainText>(Assert.Single(span.FormattedSpans));
                Assert.Equal("this is italic", formattedSpan.Text);
                Assert.Equal("_this is italic_", span.OriginalText);
            },
            s => Assert.Equal(" text", Assert.IsType<MrkdwnPlainText>(s).Text));
    }

    [Fact]
    public void CanParseInlineCodeText()
    {
        var parsed = MrkdwnParser.Parse("Hey `this is code` text");
        Assert.Collection(parsed.Spans,
            s => Assert.Equal("Hey ", Assert.IsType<MrkdwnPlainText>(s).Text),
            s => {
                var span = Assert.IsType<MrkdwnFormattedSpans>(s);
                Assert.Equal(TextFormat.Code, span.Format);
                var formattedSpan = Assert.IsType<MrkdwnPlainText>(Assert.Single(span.FormattedSpans));
                Assert.Equal("this is code", formattedSpan.Text);
                Assert.Equal("`this is code`", span.OriginalText);
            },
            s => Assert.Equal(" text", Assert.IsType<MrkdwnPlainText>(s).Text));
    }

    [Fact]
    public void CanParseStrikeThroughText()
    {
        var parsed = MrkdwnParser.Parse("Hey ~this is striked~ text");
        Assert.Collection(parsed.Spans,
            s => Assert.Equal("Hey ", Assert.IsType<MrkdwnPlainText>(s).Text),
            s => {
                var span = Assert.IsType<MrkdwnFormattedSpans>(s);
                Assert.Equal(TextFormat.Strike, span.Format);
                var formattedSpan = Assert.IsType<MrkdwnPlainText>(Assert.Single(span.FormattedSpans));
                Assert.Equal("this is striked", formattedSpan.Text);
                Assert.Equal("~this is striked~", span.OriginalText);
            },
            s => Assert.Equal(" text", Assert.IsType<MrkdwnPlainText>(s).Text));
    }

    [Fact]
    public void CanParseNestedFormattedText()
    {
        var parsed = MrkdwnParser.Parse("Hey *this _is_ bold* text");
        Assert.Collection(parsed.Spans,
            s => Assert.Equal("Hey ", Assert.IsType<MrkdwnPlainText>(s).Text),
            s => {
                var span = Assert.IsType<MrkdwnFormattedSpans>(s);
                Assert.Equal("*this _is_ bold*", span.OriginalText);
                Assert.Collection(span.FormattedSpans,
                    i => Assert.Equal("this ", Assert.IsType<MrkdwnPlainText>(i).Text),
                    i => {
                        var nestedSpan = Assert.IsType<MrkdwnFormattedSpans>(i);
                        Assert.Equal("_is_", nestedSpan.OriginalText);
                        var nested = Assert.IsType<MrkdwnPlainText>(Assert.Single(nestedSpan.FormattedSpans));
                        Assert.Equal("is", nested.Text);
                    },
                    i => Assert.Equal(" bold", Assert.IsType<MrkdwnPlainText>(i).Text));

            },
            s => Assert.Equal(" text", Assert.IsType<MrkdwnPlainText>(s).Text));
    }

    [Theory]
    [InlineData(":sparkles:", "sparkles")]
    [InlineData(":+1:", "+1")]
    public void CanParseEmoji(string text, string emojiName)
    {
        var parsed = MrkdwnParser.Parse(text);
        Assert.Equal(new MrkdwnEmoji(text, emojiName), parsed.Spans.Single());
    }

    [Theory]
    [InlineData("<https://www.google.com|Google")]
    [InlineData("<!fart>")]
    [InlineData("<!subteam!!!>")]
    [InlineData(":spa rkles:")]
    public void IgnoresMalformedTokens(string text)
    {
        var parsed = MrkdwnParser.Parse(text);
        Assert.Equal(new MrkdwnPlainText(text), parsed.Spans.Single());
    }
}
