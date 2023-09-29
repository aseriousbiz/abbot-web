using System.Runtime.Serialization;
using Serious.EntityFrameworkCore.ValueConverters;
using Xunit;

public class EnumMemberValueConverterTests
{
    public enum TestEnum
    {
        None = 0,
        [EnumMember(Value = "the-first-of-her-name")]
        First,
        [EnumMember(Value = "number-two")]
        Second,
        Third,
    }

    public class ThConvertFromProviderMethod
    {
        [Theory]
        [InlineData("the-first-of-her-name", TestEnum.First)]
        [InlineData("number-two", TestEnum.Second)]
        [InlineData("Third", TestEnum.Third)]
        [InlineData("Unknown", 0)]
        public void ReturnsStoredStringAsEnumValue(string storedValue, TestEnum expectedValue)
        {
            var converter = new EnumMemberValueConverter<TestEnum>();

            var result = converter.ConvertFromProvider(storedValue);

            var retrieved = Assert.IsType<TestEnum>(result);
            Assert.Equal(expectedValue, retrieved);
        }

        [Fact]
        public void ReturnsUnexpectedStringAsZeroValue()
        {
            var converter = new EnumMemberValueConverter<TestEnum>();

            var result = converter.ConvertFromProvider("Unknown");

            var retrieved = Assert.IsType<TestEnum>(result);
            Assert.Equal(TestEnum.None, retrieved);
        }
    }

    public class TheConvertToProviderMethod
    {
        [Theory]
        [InlineData(TestEnum.First, "the-first-of-her-name")]
        [InlineData(TestEnum.Second, "number-two")]
        [InlineData(TestEnum.Third, "Third")]
        public void ReturnsStoredStringAsEnumValue(TestEnum value, string expectedStored)
        {
            var converter = new EnumMemberValueConverter<TestEnum>();

            var result = converter.ConvertToProvider(value);

            var stored = Assert.IsType<string>(result);
            Assert.Equal(expectedStored, stored);
        }

        [Fact]
        public void StoresUnknownEnumAsEmptyString()
        {
            var converter = new EnumMemberValueConverter<TestEnum>();

            var result = converter.ConvertToProvider((TestEnum)42);

            var stored = Assert.IsType<string>(result);
            Assert.Equal(string.Empty, stored);
        }
    }
}
