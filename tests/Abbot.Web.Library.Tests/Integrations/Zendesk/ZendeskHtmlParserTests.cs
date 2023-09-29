using Serious.Abbot.Integrations.Zendesk;
using Serious.Slack.BlockKit;
using Xunit;

public class ZendeskHtmlParserTests
{
    public class TheParseHtmlMethod
    {
        [Theory]
        [InlineData("Oh hey, <strong>bold stuff</strong>", "Oh hey, *bold stuff*")]
        [InlineData("<b>bold</b>", "*bold*")]
        [InlineData("<i>italics</i>", "_italics_")]
        [InlineData("<em>italics</em>", "_italics_")]
        [InlineData("<em><strong>italicsbold</strong></em>", "_*italicsbold*_")]
        [InlineData("<strong><em>bolditalics</em></strong>", "*_bolditalics_*")]
        [InlineData("<i><strong>italicsbold</strong></i>", "_*italicsbold*_")]
        [InlineData("<strong><i>bolditalics</i></strong>", "*_bolditalics_*")]
        [InlineData("<code>code</code>", "`code`")]
        [InlineData("<u>underline</u>", "underline")]
        [InlineData("""<a rel="noopener noreferrer" href="https://ab.bot/">Anchor Tag</a>""", "<https://ab.bot/|Anchor Tag>")]
        [InlineData("""<a rel="noopener noreferrer" href="https://ab.bot/">Hey <i><strong>there</strong></i> <span style="background-color: #D1E8DF; color: #BC5A10">this</span> is <strong>bold</strong></a>""",
            "<https://ab.bot/|Hey _*there*_ this is *bold*>")]
        public void ParsesSimpleInlineHtmlIntoMrkdwn(string html, string expected)
        {
            var htmlText = $$"""
<div class="zd-comment" dir="auto">{{html}}</div>
""";
            var blocks = ZendeskHtmlParser.ParseHtml(htmlText);

            var section = Assert.IsType<Section>(Assert.Single(blocks));
            var mrkdwnText = Assert.IsType<MrkdwnText>(section.Text);
            Assert.Equal(expected, mrkdwnText.Text);
        }

        [Fact]
        public void ParseHeaders()
        {
            const string html = """
<div class="zd-comment" dir="auto">Test headers<h1 dir="auto">Heading 1</h1>Some <strong>bold</strong> text<h2 dir="auto">Heading 2</h1></div>
""";
            var blocks = ZendeskHtmlParser.ParseHtml(html);

            var expected = """
Test headers
*Heading 1*
Some *bold* text
*Heading 2*

""";
            var section = Assert.IsType<Section>(Assert.Single(blocks));
            var mrkdwnText = Assert.IsType<MrkdwnText>(section.Text);
            Assert.Equal(expected, mrkdwnText.Text);
        }

        [Fact]
        public void ParseIndentation()
        {
            const string html = """
<div class="zd-comment" dir="auto">Test pre-indentation<br><div class="zd-indent" style="margin-left: 20px"><p dir="auto">Indent 1</p></div><div class="zd-indent" style="margin-left: 40px"><p dir="auto">Indent 2</p></div>
""";
            var blocks = ZendeskHtmlParser.ParseHtml(html);

            var expected = """
Test pre-indentation
    Indent 1
        Indent 2

""";
            var section = Assert.IsType<Section>(Assert.Single(blocks));
            var mrkdwnText = Assert.IsType<MrkdwnText>(section.Text);
            Assert.Equal(expected, mrkdwnText.Text);
        }

        [Fact]
        public void ParseUnorderedList()
        {
            var html = """
<div class="zd-comment" dir="auto">Test bullet list<br><ul dir="auto"><li>Bullet list item 1</li><li>List item 2</li><li>List item 3</li></ul></div>
""";
            var blocks = ZendeskHtmlParser.ParseHtml(html);

            var section = Assert.IsType<Section>(Assert.Single(blocks));
            var mrkdwnText = Assert.IsType<MrkdwnText>(section.Text);
            Assert.Equal("""
Test bullet list

* Bullet list item 1
* List item 2
* List item 3

"""
                , mrkdwnText.Text);
        }

        [Fact]
        public void ParseOrderedList()
        {
            var html = """
<div class="zd-comment" dir="auto">Test bullet list<br><ol dir="auto"><li>Bullet list item 1</li><li>List item 2</li><li>List item 3</li></ol></div>
""";
            var blocks = ZendeskHtmlParser.ParseHtml(html);

            var section = Assert.IsType<Section>(Assert.Single(blocks));
            var mrkdwnText = Assert.IsType<MrkdwnText>(section.Text);
            Assert.Equal("""
Test bullet list

1. Bullet list item 1
2. List item 2
3. List item 3

"""
                , mrkdwnText.Text);
        }


        [Fact]
        public void ParseNestedOrderedList()
        {
            var html = """
<div class="zd-comment" dir="auto">Before listing<br><ol dir="auto"><li>One</li><li>Two<ol dir="auto"><li>Three</li><li>Four</li></ol></li><li>Five</li></ol>After listing<br></div>
""";
            var blocks = ZendeskHtmlParser.ParseHtml(html);

            var section = Assert.IsType<Section>(Assert.Single(blocks));
            var mrkdwnText = Assert.IsType<MrkdwnText>(section.Text);
            Assert.Equal("""
Before listing

1. One
2. Two
    1. Three
    2. Four
3. Five
After listing

"""
                , mrkdwnText.Text);
        }


        [Fact]
        public void ParseNestedUnorderedList()
        {
            var html = """
<div class="zd-comment" dir="auto"><ul dir="auto"><li>One</li><li>Two<ul dir="auto"><li>Three</li><li>Four</li></ul></li></ul>Testing<br></div>
""";
            var blocks = ZendeskHtmlParser.ParseHtml(html);

            var section = Assert.IsType<Section>(Assert.Single(blocks));
            var mrkdwnText = Assert.IsType<MrkdwnText>(section.Text);
            Assert.Equal("""

* One
* Two
    * Three
    * Four
Testing

"""
                , mrkdwnText.Text);
        }


        [Fact]
        public void ParsesMultilineCodeBlock()
        {
            const string html = """
<div class="zd-comment" dir="auto"><pre><code>public readonly record Foo(string Bar) {<br>    /// &lt;summary&gt;<br>    /// This is for demonstration purposes.<br>    /// &lt;/summary&gt;<br>    public static string GetValue() =&gt; "Value";<br>}</code></pre></div>
""";
            var blocks = ZendeskHtmlParser.ParseHtml(html);

            var section = Assert.IsType<Section>(Assert.Single(blocks));
            var mrkdwnText = Assert.IsType<MrkdwnText>(section.Text);
            Assert.Equal("""
```
public readonly record Foo(string Bar) {
    /// &lt;summary&gt;
    /// This is for demonstration purposes.
    /// &lt;/summary&gt;
    public static string GetValue() =&gt; "Value";
}
```
"""
                , mrkdwnText.Text);
        }

        [Fact]
        public void ParsesMultilineBlockquote()
        {
            const string html = """
<div class="zd-comment" dir="auto">A line before the blockquote.<br><blockquote><p dir="auto">Actually <strong>blockquote</strong> something.</p><p dir="auto">Line 2 of the blockquote.</p></blockquote></div>
""";
            var blocks = ZendeskHtmlParser.ParseHtml(html);

            var section = Assert.IsType<Section>(Assert.Single(blocks));
            var mrkdwnText = Assert.IsType<MrkdwnText>(section.Text);
            Assert.Equal("""
A line before the blockquote.
> Actually *blockquote* something.
> Line 2 of the blockquote.

"""
                , mrkdwnText.Text);
        }

        [Fact]
        public void ParsesFullCommentToMrkdwn()
        {
            const string html = """
<div class="zd-comment" dir="auto">Ok, here's some <code>code</code> in a <strong>blockquote</strong> coming up.<br>&nbsp;<br><pre><code>public readonly record Foo(string Bar) {<br>    /// &lt;summary&gt;<br>    /// This is for demonstration purposes.<br>    /// &lt;/summary&gt;<br>    public static string GetValue() =&gt; "Value";<br>}</code></pre>&nbsp;<br>And here's a non code blockquote.<br>&nbsp;<br><pre><code>I'm watching Between Two Ferns movie<br>As I test this out.<br>And try things.</code></pre></div>
""";
            var blocks = ZendeskHtmlParser.ParseHtml(html);

            var section = Assert.IsType<Section>(Assert.Single(blocks));
            var mrkdwnText = Assert.IsType<MrkdwnText>(section.Text);
            Assert.Equal($$"""
Ok, here's some `code` in a *blockquote* coming up.
{{MrkdwnTag.Space}}
```
public readonly record Foo(string Bar) {
    /// &lt;summary&gt;
    /// This is for demonstration purposes.
    /// &lt;/summary&gt;
    public static string GetValue() =&gt; "Value";
}
```{{MrkdwnTag.Space}}
And here's a non code blockquote.
{{MrkdwnTag.Space}}
```
I'm watching Between Two Ferns movie
As I test this out.
And try things.
```
"""
                , mrkdwnText.Text);
        }
    }
}
