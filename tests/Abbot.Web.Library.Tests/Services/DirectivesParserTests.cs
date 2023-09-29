using Serious.Abbot.AI;
using Serious.Abbot.Services;

public class DirectivesParserTests
{
    public class TheTryParseMethod
    {
        [Fact]
        public void ReturnsDirectiveContainerWithParsedTextAndDirectivesLegacy()
        {
            // Note: We only generate directives when determining whether to raise signals,
            // but the non-deterministic nature of AI could accidentally inject other text in the summary,
            // hence the other crap we're testing here.
            const string summary = "Hey, I summarized the messages.{@signal(name args)}{@signal-two(name2 args2)}{@garbage.signal(no-match)}and {@noargs}crap.";

            var result = DirectivesParser.Parse(summary);

            var names = result.Select(d => d.Name).ToArray();
            var args = result.Select(d => d.Arguments.ToArray()).ToArray();
            Assert.Equal(new[] { "signal", "signal-two", "noargs" }, names);
            Assert.Equal(new string[][]
            {
                new[] { "name", "args" },
                new[] { "name2", "args2" },
                Array.Empty<string>(),
            }, args);
        }

        [Fact]
        public void ReturnsDirectiveContainerWithParsedTextAndDirectives()
        {
            // Note: We only generate directives when determining whether to raise signals,
            // but the non-deterministic nature of AI could accidentally inject other text in the summary,
            // hence the other crap we're testing here.
            const string summary = "Hey, I summarized the messages.[!signal:name args][!signal-two:name2 args2][garbage.signal:no-match][more-garbage]and [!noargs] crap.";

            var result = DirectivesParser.Parse(summary);

            var names = result.Select(d => d.Name).ToArray();
            var args = result.Select(d => d.Arguments.ToArray()).ToArray();
            Assert.Equal(new[] { "signal", "signal-two", "noargs" }, names);
            Assert.Equal(new string[][]
            {
                new[] { "name", "args" },
                new[] { "name2", "args2" },
                Array.Empty<string>(),
            }, args);
        }
    }
}
