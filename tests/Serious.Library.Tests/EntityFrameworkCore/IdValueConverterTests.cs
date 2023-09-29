using Serious;
using Serious.EntityFrameworkCore;
using Xunit;

public class IdValueConverterTests
{
    public class ThConvertFromProviderMethod
    {
        [Fact]
        public void ReturnsStoredIntegerAsId()
        {
            var converter = new IdValueConverter<string>();

            var result = converter.ConvertFromProvider(23);

            var retrieved = Assert.IsType<Id<string>>(result);
            Assert.Equal(23, retrieved.Value);
        }
    }

    public class TheConvertToProviderMethod
    {
        [Fact]
        public void ConvertsIntegerToId()
        {
            var id = new Id<object>(42);
            var converter = new IdValueConverter<object>();

            var result = converter.ConvertToProvider(id);

            var retrieved = Assert.IsType<int>(result);
            Assert.Equal(42, retrieved);
        }
    }
}
