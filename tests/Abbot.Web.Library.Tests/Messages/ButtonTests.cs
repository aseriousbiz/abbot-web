using Serious.Abbot.Scripting;
using Xunit;

public class ButtonTests
{
    public class TheConstructor
    {
        [Fact]
        public void PopulatesTitleAndValueFromTitle()
        {
            var button = new Button("title");

            Assert.Equal("title", button.Title);
            Assert.Equal("title", button.Arguments);
        }

        [Fact]
        public void PopulatesTitleAndValue()
        {
            var button = new Button("title", "value");

            Assert.Equal("title", button.Title);
            Assert.Equal("value", button.Arguments);
        }
    }
}
