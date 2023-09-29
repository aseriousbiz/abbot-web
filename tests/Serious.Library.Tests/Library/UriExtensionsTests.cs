using System;
using Serious;
using Xunit;

public class UriExtensionsTests
{
    public class TheAppendMethod
    {
        [Theory]
        [InlineData("https://localhost", "foo", "https://localhost/foo")]
        [InlineData("https://localhost/", "foo", "https://localhost/foo")]
        [InlineData("https://localhost/", "/foo", "https://localhost/foo")]
        [InlineData("https://localhost/api", "foo", "https://localhost/api/foo")]
        [InlineData("https://localhost/api", "/foo", "https://localhost/api/foo")]
        [InlineData("https://localhost/api/", "foo", "https://localhost/api/foo")]
        [InlineData("https://localhost/api/", "/foo", "https://localhost/api/foo")]
        public void AppendsTheWayWeThinkItShould(string baseUrl, string appendage, string expected)
        {
            var url = new Uri(baseUrl);

            var result = url.Append(appendage);

            Assert.Equal(expected, result.ToString());
        }
    }
}
