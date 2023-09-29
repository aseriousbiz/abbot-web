using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Humanizer;
using Serious;
using Xunit;

public class EnumExtensionsTests
{
    public enum TestEnum
    {
        NoDisplay = 1,

        [Display]
        [EnumMember]
        HasDisplayNoName,

        [Display(Name = "Has Display Name")]
        [EnumMember(Value = "Display Name")]
        HasDisplayName,
    }

    public class GetDisplayNameMethod
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData(default(TestEnum), "")]
        [InlineData(TestEnum.NoDisplay, nameof(TestEnum.NoDisplay))]
        [InlineData(TestEnum.HasDisplayNoName, nameof(TestEnum.HasDisplayNoName))]
        [InlineData(TestEnum.HasDisplayName, "Has Display Name")]
        public void Works(TestEnum? value, string expected)
        {
            Assert.Equal(expected, value.GetDisplayName());
        }

        [Theory]
        [InlineData(default(TestEnum), "0")]
        [InlineData(TestEnum.NoDisplay, "No display")]
        [InlineData(TestEnum.HasDisplayNoName, "Has display no name")]
        [InlineData(TestEnum.HasDisplayName, "Has Display Name")]
        public void IsDifferentFromHumanize(TestEnum? value, string expected)
        {
            Assert.Equal(expected, value.Humanize());
        }
    }

    public class TheGetEnumMemberValueNameMethod
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData(TestEnum.NoDisplay, nameof(TestEnum.NoDisplay))]
        [InlineData(TestEnum.HasDisplayNoName, nameof(TestEnum.HasDisplayNoName))]
        [InlineData(TestEnum.HasDisplayName, "Display Name")]
        public void GetsEnumMemberValue(TestEnum? value, string expected)
        {
            Assert.Equal(expected, value.GetEnumMemberValueName());
        }
    }
}
