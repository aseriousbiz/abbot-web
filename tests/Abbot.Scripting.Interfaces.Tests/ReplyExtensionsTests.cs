public class ReplyExtensionsTests
{
    public class TheToMarkdownListMethod
    {
        [Fact]
        public void ReturnsBulletedList()
        {
            var items = new[] { "one", "two", "three" };

            var result = items.ToMarkdownList();

            Assert.Equal(@"• one
• two
• three", result);
        }

        [Fact]
        public void ReturnsEmptyStringForEmptyList()
        {
            var items = new string[] { };

            var result = items.ToMarkdownList();

            Assert.Empty(result);
        }
    }

    public class TheToOrderedListMethod
    {
        [Fact]
        public void ReturnsOrderedList()
        {
            var items = new[] { "one", "two", "three" };

            var result = items.ToOrderedList();

            Assert.Equal(@"1. one
2. two
3. three", result);
        }
    }
}
