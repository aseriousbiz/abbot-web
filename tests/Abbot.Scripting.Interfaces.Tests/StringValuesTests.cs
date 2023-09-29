public class StringValuesTests
{
    public class TheImplicitConversionOperator
    {
        [Fact]
        public void CanConvertToString()
        {
            var value = new StringValues("one");

            string? result = value;

            Assert.Equal("one", result);
        }
    }
}
