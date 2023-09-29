using Serious.Filters;
using Xunit;

public class FilterTests
{
    public class TheCreateMethod
    {
        [Theory]
        [InlineData("name", "value", "name:value", true)]
        [InlineData("-name", "value", "-name:value", false)]
        [InlineData("name", "the value", "name:\"the value\"", true)]
        public void CreatesFilter(string name, string value, string expectedOriginalText, bool expectedInclude)
        {
            var filter = Filter.Create(name, value);

            Assert.Equal(expectedInclude, filter.Include);
            Assert.Equal("name", filter.Field);
            Assert.Equal(value, filter.Value);
            Assert.Equal(expectedOriginalText, filter.OriginalText);
        }
    }
}
