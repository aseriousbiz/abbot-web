using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;

public class ArgumentsTests
{
    public class TheConstructor
    {
        [Fact]
        public void SplitsArgumentsBasedOnSpaces()
        {
            const string arguments = "one two three";

            var tokenized = new Arguments(arguments)
                .Cast<Argument>()
                .ToList();

            Assert.Equal(3, tokenized.Count);
            Assert.Equal("one", tokenized[0].Value);
            Assert.Equal("one", tokenized[0].OriginalText);
            Assert.Equal("two", tokenized[1].Value);
            Assert.Equal("two", tokenized[1].OriginalText);
            Assert.Equal("three", tokenized[2].Value);
            Assert.Equal("three", tokenized[2].OriginalText);
        }

        [Fact]
        public void ParsesIntArgument()
        {
            const string arguments = "1 2 3";

            var tokenized = new Arguments(arguments)
                .Cast<Int32Argument>()
                .ToList();

            Assert.Equal(3, tokenized.Count);
            Assert.Equal(1, tokenized[0].Int32Value);
            Assert.Equal(2, tokenized[1].Int32Value);
            Assert.Equal(3, tokenized[2].Int32Value);
        }

        [Fact]
        public void SplitsArgumentsBasedOnQuotes()
        {
            const string arguments = "one \"two three\" four";

            var tokenized = new Arguments(arguments)
                .Cast<Argument>()
                .ToList();

            Assert.Equal(3, tokenized.Count);
            Assert.Equal("one", tokenized[0].Value);
            Assert.Equal("one", tokenized[0].OriginalText);
            Assert.Equal("two three", tokenized[1].Value);
            Assert.Equal("\"two three\"", tokenized[1].OriginalText);
            Assert.Equal("four", tokenized[2].Value);
            Assert.Equal("four", tokenized[2].OriginalText);
        }

        [Theory]
        [InlineData("one <@Paul Nakata> four", "<@Paul Nakata>")]
        [InlineData("one <@U013WCHH9NU> four", "<@U013WCHH9NU>")]
        [InlineData("one <@785942243508617256> four", "<@785942243508617256>")]
        public void CanTokenizeMentions(string arguments, string userMention)
        {
            var tokenized = new Arguments(arguments)
                .Cast<Argument>()
                .ToList();

            Assert.Equal(3, tokenized.Count);
            Assert.Equal("one", tokenized[0].Value);
            Assert.Equal("one", tokenized[0].OriginalText);
            Assert.Equal(userMention, tokenized[1].Value);
            Assert.Equal(userMention, tokenized[1].OriginalText);
            Assert.Equal("four", tokenized[2].Value);
            Assert.Equal("four", tokenized[2].OriginalText);
        }

        [Theory]
        [InlineData("one <#808770225980833835> four", "<#808770225980833835>", "808770225980833835", "")]
        [InlineData("one <#C0141FZEY56|playground> four", "<#C0141FZEY56|playground>", "C0141FZEY56", "playground")]
        public void CanTokenizeRoomMentions(string arguments, string roomMention, string roomId, string roomName)
        {
            var tokenized = new Arguments(arguments)
                .Cast<Argument>()
                .ToList();

            var roomArgument = Assert.IsType<RoomArgument>(tokenized[1]);
            Assert.Equal(3, tokenized.Count);
            Assert.Equal("one", tokenized[0].Value);
            Assert.Equal("one", tokenized[0].OriginalText);
            Assert.Equal(roomMention, roomArgument.Value);
            Assert.Equal(roomMention, roomArgument.OriginalText);
            Assert.Equal("four", tokenized[2].Value);
            Assert.Equal("four", tokenized[2].OriginalText);
            Assert.Equal(roomId, roomArgument.Room.Id);
            Assert.Equal(roomName, roomArgument.Room.Name);
        }

        [Theory]
        [InlineData("one <@Paul Nakata> four", "<@Paul Nakata>", "Paul Nakata")]
        [InlineData("one <@U013WCHH9NU> four", "<@U013WCHH9NU>", "U013WCHH9NU")]
        [InlineData("one <@785942243508617256> four", "<@785942243508617256>", "785942243508617256")]
        public void CanMapMentions(string arguments, string userMention, string platformUserId)
        {
            var mentions = new List<PlatformUser>
            {
                new(platformUserId, "pmn", "pmn")
            };
            var tokenized = new Arguments(arguments, mentions)
                .Cast<Argument>()
                .ToList();

            Assert.Equal(3, tokenized.Count);
            Assert.Equal("one", tokenized[0].Value);
            Assert.Equal("one", tokenized[0].OriginalText);
            var mentionArgument = Assert.IsType<MentionArgument>(tokenized[1]);
            Assert.Equal(userMention, mentionArgument.Value);
            Assert.Equal(userMention, mentionArgument.OriginalText);
            Assert.Equal("four", tokenized[2].Value);
            Assert.Equal("four", tokenized[2].OriginalText);
        }

        [Fact]
        public void HandlesDuplicateMentions()
        {
            var mentions = new List<PlatformUser>
            {
                new("U013WCHH9NU", "pmn", "pmn"),
                new("U013WCHH9NU", "pmn", "pmn")
            };
            var tokenized = new Arguments("Hey <@U013WCHH9NU>! Hey <@U013WCHH9NU>!", mentions)
                .Cast<Argument>()
                .ToList();

            Assert.Collection(tokenized,
                a => Assert.IsType<Argument>(a),
                a => Assert.IsType<MentionArgument>(a),
                a => Assert.IsType<Argument>(a),
                a => Assert.IsType<Argument>(a),
                a => Assert.IsType<MentionArgument>(a),
                a => Assert.IsType<Argument>(a));
        }

        [Fact]
        public void SplitsArgumentsBasedOnSingleQuotes()
        {
            const string arguments = "one 'two three' four";

            var tokenized = new Arguments(arguments)
                .Cast<Argument>()
                .ToList();

            Assert.Equal(3, tokenized.Count);
            Assert.Equal("one", tokenized[0].Value);
            Assert.Equal("one", tokenized[0].OriginalText);
            Assert.Equal("two three", tokenized[1].Value);
            Assert.Equal("'two three'", tokenized[1].OriginalText);
            Assert.Equal("four", tokenized[2].Value);
            Assert.Equal("four", tokenized[2].OriginalText);
        }

        [Theory]
        [InlineData("https://aseriousbiz.slack.com/team/U02M9M339R9\tA dm via U link", new[] { "https://aseriousbiz.slack.com/team/U02M9M339R9", "A", "dm", "via", "U", "link" })]
        [InlineData("https://aseriousbiz.slack.com/team/U02M9M339R9\nA dm via U link", new[] { "https://aseriousbiz.slack.com/team/U02M9M339R9", "A", "dm", "via", "U", "link" })]
        // "\u00A0" is a no-break space ("&nbsp;" in XML, etc.). Slack sometimes inserts one after pasting a URL.
        [InlineData("https://aseriousbiz.slack.com/team/U02M9M339R9\u00A0A dm via U link", new[] { "https://aseriousbiz.slack.com/team/U02M9M339R9", "A", "dm", "via", "U", "link" })]
        public void SplitsArgumentsUsingNonStandardSpaces(string input, string[] arguments)
        {
            var tokenized = new Arguments(input)
                .Cast<Argument>()
                .Select(a => a.Value)
                .ToArray();

            Assert.Equal(arguments, tokenized);
        }

        [Theory]
        [InlineData("foo <https://aseriousbusiness.com> bar <https://haacked.com/> ", "foo https://aseriousbusiness.com bar https://haacked.com/")]
        [InlineData("meta[name=‘description’]", "meta[name='description']")]
        [InlineData("<https://aseriousbusiness.com>", "https://aseriousbusiness.com")]
        [InlineData("<https://aseriousbusiness.com|https://aseriousbusiness.com>", "https://aseriousbusiness.com")]
        [InlineData("foo<https://aseriousbusiness.com>bar", "foo<https://aseriousbusiness.com>bar")]
        [InlineData("foo < https://aseriousbusiness.com > bar", "foo < https://aseriousbusiness.com > bar")]
        [InlineData(@"new Uri(""<https://aseriousbusiness.com>"")", @"new Uri(""https://aseriousbusiness.com"")")]
        [InlineData(@"new Uri(“<https://aseriousbusiness.com>”)", @"new Uri(""https://aseriousbusiness.com"")")]
        public void NormalizesArguments(string message, string expectedArguments)
        {
            var result = new Arguments(message);

            Assert.Equal(expectedArguments, result.Value);
        }
    }

    public class TheDeconstructMethod
    {
        [Fact]
        public void CanDeconstructTwoArguments()
        {
            IArguments tokenized = new Arguments("one two");

            var (one, two) = tokenized;

            Assert.Equal("one", one.Value);
            Assert.Equal("two", two.Value);
        }

        [Fact]
        public void CanDeconstructThreeArguments()
        {
            IArguments tokenized = new Arguments("one two three four");

            var (one, two, three) = tokenized;

            Assert.Equal("one", one.Value);
            Assert.Equal("two", two.Value);
            Assert.Equal("three four", three.Value);
        }

        [Fact]
        public void CanDeconstructFourArguments()
        {
            IArguments tokenized = new Arguments("one two three four");

            var (one, two, three, four) = tokenized;

            Assert.Equal("one", one.Value);
            Assert.Equal("two", two.Value);
            Assert.Equal("three", three.Value);
            Assert.Equal("four", four.Value);
        }

        [Fact]
        public void CanDeconstructFiveArguments()
        {
            IArguments tokenized = new Arguments("one two three four five");

            var (one, two, three, four, five) = tokenized;

            Assert.Equal("one", one.Value);
            Assert.Equal("two", two.Value);
            Assert.Equal("three", three.Value);
            Assert.Equal("four", four.Value);
            Assert.Equal("five", five.Value);
        }

        [Fact]
        public void CanDeconstructSixArguments()
        {
            IArguments tokenized = new Arguments("one two three four five six");

            var (one, two, three, four, five, six) = tokenized;

            Assert.Equal("one", one.Value);
            Assert.Equal("two", two.Value);
            Assert.Equal("three", three.Value);
            Assert.Equal("four", four.Value);
            Assert.Equal("five", five.Value);
            Assert.Equal("six", six.Value);
        }

        [Fact]
        public void DeconstructsRestArgumentCollection()
        {
            IArguments tokenized = new Arguments("one two three four");

            var (one, two, three) = tokenized;

            Assert.Equal("one", one.Value);
            Assert.Equal("two", two.Value);
            Assert.Equal("three four", three.Value);
        }

        [Fact]
        public void DeconstructsRestQuotedArgumentCollection()
        {
            IArguments tokenized = new Arguments("one two \"three and four\" five");

            var (one, two, three) = tokenized;

            Assert.Equal("one", one.Value);
            Assert.Equal("two", two.Value);
            Assert.Equal("\"three and four\" five", three.Value);
        }

        [Fact]
        public void CanDeconstructMoreArgumentsThanPresent()
        {
            IArguments tokenized = new Arguments("one two");

            var (one, two, three) = tokenized;

            Assert.Equal("one", one.Value);
            Assert.Equal("two", two.Value);
            Assert.IsAssignableFrom<IMissingArgument>(three);
        }
    }

    public class TheValueProperty
    {
        [Fact]
        public void ReturnsArgumentsAsOriginalRawArguments()
        {
            IArguments tokenized = new Arguments("one \"two and three\" four");

            var result = tokenized.Value;

            Assert.Equal("one \"two and three\" four", result);
            Assert.Equal("one", tokenized[0].Value);
            Assert.Equal("two and three", tokenized[1].Value);
            Assert.Equal("four", tokenized[2].Value);
        }

        [Fact]
        public void ReturnsRestArgumentsAsConcatenatedString()
        {
            IArguments tokenized = new Arguments("one \"two and three\" four");
            var (_, rest) = tokenized;

            var result = rest.Value;

            Assert.Equal("\"two and three\" four", result);
            var restArgs = rest as IArguments;
            Assert.NotNull(restArgs);
            Assert.Equal("two and three", restArgs[0].Value);
            Assert.Equal("four", restArgs[1].Value);
        }
    }

    public class ThePrependMethod
    {
        [Fact]
        public void PrependsArguments()
        {
            var arguments = new Arguments("one \"two and three\" four");

            arguments.Prepend("\"negative one\" zero");

            Assert.Equal(5, arguments.Count);
            Assert.Equal("negative one", arguments[0].Value);
            Assert.Equal("zero", arguments[1].Value);
            Assert.Equal("one", arguments[2].Value);
            Assert.Equal("two and three", arguments[3].Value);
            Assert.Equal("four", arguments[4].Value);
        }
    }

    public class ThePopMethod
    {
        [Fact]
        public void ReturnsEmptyArgumentsWhenNoArguments()
        {
            var arguments = new Arguments("");

            var (_, rest) = arguments.Pop();

            Assert.Empty(rest);
            var (_, rest2) = rest.Pop();
            Assert.Empty(rest2);
        }

        [Fact]
        public void ReturnsSkillNameAndRestOfArguments()
        {
            var arguments = new Arguments("skill");

            var (skill, rest) = arguments.Pop();

            Assert.Equal("skill", skill);
            Assert.Empty(rest);
        }

        [Fact]
        public void ReturnsSkillAndArgs()
        {
            var arguments = new Arguments("skill one two");

            var (skill, rest) = arguments.Pop();

            Assert.Equal("skill", skill);
            var (one, two) = rest;
            Assert.Equal("one", one.Value);
            Assert.Equal("two", two.Value);
        }
    }

    public class TheSkipMethod
    {
        [Fact]
        public void CanSkipOneArgument()
        {
            var arguments = new Arguments("skill one two");

            var (one, two) = arguments.Skip(1);

            Assert.Equal("one", one.Value);
            Assert.Equal("two", two.Value);
        }

        [Fact]
        public void CanSkipTwoArguments()
        {
            var arguments = new Arguments("skill one two three");

            var (two, three) = arguments.Skip(2);

            Assert.Equal("two", two.Value);
            Assert.Equal("three", three.Value);
        }

        [Fact]
        public void ReturnsSameWhenSkippingZero()
        {
            var arguments = new Arguments("skill one two three");

            var result = arguments.Skip(0);

            Assert.Same(result, arguments);
        }

        [Fact]
        public void CanSkipAllArguments()
        {
            var arguments = new Arguments("skill one two three");

            var result = arguments.Skip(5);

            Assert.Same(result, Arguments.Empty);
        }
    }

    public class TheFindAndRemoveMethod
    {
        [Fact]
        public void ReturnsEmptyArgumentsWhenNoArguments()
        {
            var arguments = new Arguments("");

            var (found, rest) = arguments.FindAndRemove(_ => true);

            Assert.Empty(rest);
            Assert.IsAssignableFrom<IMissingArgument>(found);
            Assert.Empty(rest);
        }

        [Fact]
        public void ReturnsFoundArgumentsAndRest()
        {
            var arguments = new Arguments("one two three four");

            var (found, rest) = arguments.FindAndRemove(a => a.Value == "three");

            Assert.Equal("three", found.Value);
            Assert.Equal(3, rest.Count);
            Assert.Equal("one two four", rest.Value);
        }
    }

    public class TheRangeIndexer
    {
        [Fact]
        public void ReturnsEntireArguments()
        {
            var arguments = new Arguments("one two three four");

            var result = arguments[..];

            Assert.Equal(new[] { "one", "two", "three", "four" }, result.Select(a => a.Value).ToArray());
        }

        [Fact]
        public void ReturnsTail()
        {
            var arguments = new Arguments("one two three four");

            var result = arguments[2..];

            Assert.Equal(new[] { "three", "four" }, result.Select(a => a.Value).ToArray());
        }

        [Fact]
        public void ReturnsFront()
        {
            var arguments = new Arguments("one two three four");

            var result = arguments[..^1];

            Assert.Equal(new[] { "one", "two", "three" }, result.Select(a => a.Value).ToArray());
        }

        [Fact]
        public void ReturnsMiddle()
        {
            var arguments = new Arguments("one two three four");

            var result = arguments[1..^1];

            Assert.Equal(new[] { "two", "three" }, result.Select(a => a.Value).ToArray());
        }
    }

    public class TheSliceMethod
    {
        [Fact]
        public void ReturnsEntireArguments()
        {
            var arguments = new Arguments("one two three four");

            var result = arguments.Slice(0, 4);

            Assert.Equal(new[] { "one", "two", "three", "four" }, result.Select(a => a.Value).ToArray());
        }

        [Fact]
        public void ReturnsTail()
        {
            var arguments = new Arguments("one two three four");

            var result = arguments.Slice(2, 2);

            Assert.Equal(new[] { "three", "four" }, result.Select(a => a.Value).ToArray());
        }

        [Fact]
        public void ReturnsFront()
        {
            var arguments = new Arguments("one two three four");

            var result = arguments.Slice(0, 3);

            Assert.Equal(new[] { "one", "two", "three" }, result.Select(a => a.Value).ToArray());
        }

        [Fact]
        public void ReturnsMiddle()
        {
            var arguments = new Arguments("one two three four");

            var result = arguments.Slice(1, 2);

            Assert.Equal(new[] { "two", "three" }, result.Select(a => a.Value).ToArray());
        }

        [Fact]
        public void CanPatternMatchWithRest()
        {
            var arguments = new Arguments("one two three four");

            if (arguments is [var head, .. var tail])
            {
                Assert.Equal("one", head.Value);
                Assert.Equal(new[] { "two", "three", "four" }, tail.Select(a => a.Value).ToArray());
            }
            else
            {
                Assert.Fail("Could not pattern match arguments.");
            }
        }
    }
}
