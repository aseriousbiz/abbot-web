using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.TestHelpers;
using Xunit;

public class MarkdownExtensionsTests
{
    public class TheToMarkdownListMethod
    {
        [Fact]
        public void ReturnsNamesForMemories()
        {
            var items = new[]
            {
                new Memory { Name = "key", Content = "bar"},
                new Memory { Name = "anotherKey", Content = "bar"},
                new Memory { Name = "a-third-key", Content = "bar"}
            };

            var result = items.ToMarkdownList();

            Assert.Equal(@"• `key`
• `anotherKey`
• `a-third-key`", result);
        }

        [Fact]
        public void ReturnsListForKeyValuePairsWithTrailingPunctuation()
        {
            var items = new[]
            {
                new FakeSkillDescriptor("key", "value"),
                new FakeSkillDescriptor("anotherKey", "another value."),
                new FakeSkillDescriptor("a-third-key", "a third key?")
            };

            var result = items.ToMarkdownList();

            Assert.Equal(@"• `key` - value.
• `anotherKey` - another value.
• `a-third-key` - a third key?", result);
        }
    }
}
