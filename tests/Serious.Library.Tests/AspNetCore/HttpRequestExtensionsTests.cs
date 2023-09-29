using Microsoft.AspNetCore.Http;
using Serious.AspNetCore;
using Xunit;

public class HttpRequestExtensionsTests
{
    public class TheAcceptsJsonMethod
    {
        [Theory]
        [InlineData("", false)]
        [InlineData("garbage sthaeosnuth aosnetuh", false)]
        [InlineData("text/json", false)]
        [InlineData("application/vnd.github+json", false)]
        [InlineData("application/json", true)]
        [InlineData("text/html, application/json", true)]
        [InlineData("text/html, application/json;q=0.9", true)]
        [InlineData("text/html, application/*;q=0.9", true)]
        [InlineData("text/html;q=0.9, */*;q=0.8", true)]
        [InlineData("text/html, application/xml;q=0.9", false)]
        public void AcceptsJson(string acceptHeader, bool expected)
        {
            var request = new DefaultHttpContext().Request;
            request.Method = HttpMethods.Get;

            request.Headers["Accept"] = acceptHeader;

            var result = request.AcceptsJson();

            Assert.Equal(expected, result);
        }
    }

    public class TheIsJsonContentTypeMethod
    {
        [Theory]
        [InlineData("", false)]
        [InlineData("text/html", false)]
        [InlineData("garbage sthaeosnuth aosnetuh", false)]
        [InlineData("text/json", true)]
        [InlineData("application/vnd.github+json", true)]
        [InlineData("application/json", true)]
        [InlineData("application/json; charset=utf8", true)]
        public void ReturnsTrueForJson(string contentType, bool expected)
        {
            var request = new DefaultHttpContext().Request;
            request.Headers["Content-Type"] = contentType;

            var result = request.IsJsonContentType();

            Assert.Equal(expected, result);
        }
    }

    public class TheIsXmlContentTypeMethod
    {
        [Theory]
        [InlineData("", false)]
        [InlineData("text/html", false)]
        [InlineData("garbage sthaeosnuth aosnetuh", false)]
        [InlineData("text/xml", true)]
        [InlineData("application/vnd.github+xml", true)]
        [InlineData("application/xml", true)]
        [InlineData("application/xml; charset=utf8", true)]
        public void ReturnsTrueForXml(string contentType, bool expected)
        {
            var request = new DefaultHttpContext().Request;
            request.Headers["Content-Type"] = contentType;

            var result = request.IsXmlContentType();

            Assert.Equal(expected, result);
        }
    }

    public class TheTryGetAcceptedContentTypeMethod
    {
        [Theory]
        [InlineData("application/xml", "application/xml", true, "application/xml")]
        [InlineData("application/xml;charset=utf8", "application/xml", true, "application/xml")]
        [InlineData("application/xml;charset=utf8", "*/*", true, "application/xml")]
        [InlineData("application/xml;charset=utf8", "text/xml", false, "application/xml")]
        [InlineData("application/json", "application/json", true, "application/json")]
        public void ReturnsContentTypeIfClientAcceptsIt(string contentType, string accepts, bool expected, string expectedValue)
        {
            var request = new DefaultHttpContext().Request;
            request.Headers["Content-Type"] = contentType;
            request.Headers["Accept"] = accepts;

            var result = request.TryGetAcceptedContentType(out var mediaType);

            Assert.Equal(expected, result);
            if (result)
            {
                Assert.Equal(expectedValue, mediaType!.MediaType);
            }
        }
    }
}
