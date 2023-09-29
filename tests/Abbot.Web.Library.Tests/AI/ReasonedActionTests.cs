using Serious.Abbot.AI;

public class ReasonedActionTests
{
    public class TheParseMethod
    {
        [Fact]
        public void ReturnsFalseWhenInputIsNotValid()
        {
            var input = "This is not a valid input.";

            var result = Reasoned.Parse(input);

            Assert.Empty(result);
        }

        [Fact]
        public void ReturnsSingleReasonedAction()
        {
            var input = """
[Thought]Customer asked for the answer to the life, universe, and everything.[/Thought]
[Action]42[/Action]
""";

            var result = Reasoned.Parse(input);

            var reasonedAction = Assert.Single(result);
            Assert.Equal(
                new("Customer asked for the answer to the life, universe, and everything.", "42"),
                reasonedAction);
        }

        [Fact]
        public void ReturnsASetOfReasonedActions()
        {
            var input = """
[Thought]Customer asked for the answer to the life, universe, and everything.[/Thought]
[Action]42[/Action]

[Thought]Customer is unhappy.[/Thought]
[Action][!sentiment:negative][/Action]
""";

            var result = Reasoned.Parse(input).ToArray();

            var expectedActions = new Reasoned<string>[]
            {
                new("Customer asked for the answer to the life, universe, and everything.", "42"),
                new("Customer is unhappy.", "[!sentiment:negative]")
            };
            Assert.Equal(expectedActions, result);
        }
    }

    public class TheExtractFirstCodeFenceMethod
    {
        [Fact]
        public void ReturnsOriginalStringOnNoFence()
        {
            Assert.Equal("This is a test", Reasoned.ExtractFirstCodeFence("This is a test"));
        }

        [Fact]
        public void ReturnsContentOfSingleCodeFence()
        {
            const string payload =
                """
                Outside the fence
                ```
                Inside the fence
                ```
                Also outside the fence
                """;
            Assert.Equal($"Inside the fence\n", Reasoned.ExtractFirstCodeFence(payload));
        }

        [Fact]
        public void ReturnsContentOfDanglingCodeFence()
        {
            const string payload =
                """
                Outside the fence
                ```
                Inside the fence
                """;
            Assert.Equal($"Inside the fence\n", Reasoned.ExtractFirstCodeFence(payload));
        }

        [Fact]
        public void IgnoresSecondCodeFence()
        {
            const string payload =
                """
                Outside the fence
                ```
                Inside the fence
                ```
                Also outside the fence
                ```
                Second code fence
                ```
                """;
            Assert.Equal($"Inside the fence\n", Reasoned.ExtractFirstCodeFence(payload));
        }

        [Fact]
        public void HandlesInnerCodeFence()
        {
            const string payload =
                """
                No fence here.
                But ``` there are some triple ticks.
                Still no fence
                """;
            Assert.Equal(
                """
                No fence here.
                But ``` there are some triple ticks.
                Still no fence
                """, Reasoned.ExtractFirstCodeFence(payload));
        }
    }
}
