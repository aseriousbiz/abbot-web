using Serious.Abbot.AI;

public class DirectiveTests
{
    public class TheParseMethod
    {
        [Theory]
        [InlineData("{@signal(name args)}", "signal", "name args", new[] { "name", "args" })]
        [InlineData("{@signal(name   args)}", "signal", "name   args", new[] { "name", "args" })]
        [InlineData("{@foo(bar baz qux)}", "foo", "bar baz qux", new[] { "bar", "baz", "qux" })]
        [InlineData("{@foo(bar \"baz qux\")}", "foo", "bar \"baz qux\"", new[] { "bar", "baz qux" })]
        [InlineData("[!signal:name args]", "signal", "name args", new[] { "name", "args" })]
        [InlineData("[!signal:name   args]", "signal", "name   args", new[] { "name", "args" })]
        [InlineData("[!foo:bar baz qux]", "foo", "bar baz qux", new[] { "bar", "baz", "qux" })]
        [InlineData("[!foo:bar \"baz qux\"]", "foo", "bar \"baz qux\"", new[] { "bar", "baz qux" })]
        [InlineData("[!foo]", "foo", "", new string[] { })]

        public void ParsesDirectives(string input, string expectedType, string expectedRawArgs, string[] expectedArgs)
        {
            var directive = Directive.Parse(input);

            Assert.Equal(expectedType, directive.Name);
            Assert.Equal(expectedRawArgs, directive.RawArguments);
            Assert.Equal(expectedArgs, directive.Arguments.ToArray());
        }
    }

    public class TheTryParseMethod
    {
        [Theory]
        [InlineData("{@signal(name args)}", "signal", "name args", new[] { "name", "args" })]
        [InlineData("[!signal:name args]", "signal", "name args", new[] { "name", "args" })]
        [InlineData("[!signal: name args ]", "signal", "name args", new[] { "name", "args" })]
        public void ReturnsTrueForGoodDirective(
            string input,
            string expectedType,
            string expectedRawArgs,
            string[] expectedArgs)
        {
            Assert.True(Directive.TryParse(input, out var directive));

            Assert.Equal(expectedType, directive.Name);
            Assert.Equal(expectedRawArgs, directive.RawArguments);
            Assert.Equal(expectedArgs, directive.Arguments.ToArray());
        }

        [Theory]
        [InlineData("This is not a directive")]
        [InlineData("[neither is this](https://example.com)")]
        [InlineData("[!neither is this](https://example.com)")]
        [InlineData("[neither: is this](https://example.com)")]
        public void ReturnsFalseForBadDirective(string input)
        {

            Assert.False(Directive.TryParse(input, out _));
        }
    }
}
