using System;
using System.Collections.Generic;
using System.Numerics;
using Serious;
using Xunit;

/// <summary>
/// Useful methods for working with types.
/// </summary>
public class ReflectionExtensionsTests
{
    public class TheGetUnderlyingNullableTypeOrTypeMethod
    {
        [Fact]
        public void ReturnsUnderlyingTypeForNullable()
        {
            var type = typeof(int?);

            var result = type.GetUnderlyingNullableTypeOrType();

            Assert.Equal(typeof(int), result);
        }

        [Fact]
        public void ReturnsTypeForNonNullable()
        {
            var type = typeof(int);

            var result = type.GetUnderlyingNullableTypeOrType();

            Assert.Equal(typeof(int), result);
        }
    }

    public class TheGetJavaScriptTypeMethod
    {
        [Theory]
        [InlineData(typeof(byte), "Number")]
        [InlineData(typeof(sbyte), "Number")]
        [InlineData(typeof(short), "Number")]
        [InlineData(typeof(ushort), "Number")]
        [InlineData(typeof(int), "Number")]
        [InlineData(typeof(nuint), "Number")]
        [InlineData(typeof(nint), "Number")]
        [InlineData(typeof(uint), "Number")]
        [InlineData(typeof(long), "Number")]
        [InlineData(typeof(ulong), "Number")]
        [InlineData(typeof(int?), "Number")]
        [InlineData(typeof(float), "Number")]
        [InlineData(typeof(double), "Number")]
        [InlineData(typeof(decimal), "Number")]
        [InlineData(typeof(Half), "Number")]
        [InlineData(typeof(bool), "Boolean")]
        [InlineData(typeof(string), "String")]
        [InlineData(typeof(BigInteger), "BigInt")]
        [InlineData(typeof(Dictionary<string, string>), "Object")]
        public void ReturnsType(Type type, string expected)
        {
            var result = type.GetJavaScriptType();
            Assert.Equal(expected, result);
        }
    }
}
