using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Serious.AspNetCore;
using Xunit;

public class HttpContextExtensionTests
{
    public class TheGetPageNameMethod
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("/", "")]
        [InlineData("/about", "about")]
        [InlineData("/about/", "about")]
        [InlineData("/about/index", "about")]
        [InlineData("/about/index/", "about")]
        [InlineData("/About/Index/", "About")]
        public void ReturnsPageName(string url, string expectedPageName)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Features.Set<IHttpRequestFeature>(
                new HttpRequestFeature
                {
                    RawTarget = url
                });

            var result = httpContext.GetPageName();

            Assert.Equal(expectedPageName, result);
        }
    }

    public class TheGetPathMethod
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("/", "/")]
        [InlineData("/about", "/about")]
        [InlineData("/about/", "/about/")]
        [InlineData("/skills/edit?test", "/skills/edit")]
        public void ReturnsPageName(string url, string expectedPageName)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Features.Set<IHttpRequestFeature>(
                new HttpRequestFeature
                {
                    RawTarget = url
                });

            var result = httpContext.GetPath();

            Assert.Equal(expectedPageName, result);
        }
    }

    public class TheGetPageInfoMethod
    {
        [Theory]
        [InlineData("", "Home", "Home", "Home")]
        [InlineData("/", "Home", "Home", "Home")]
        [InlineData("/about", "About", "Home", "Home")]
        [InlineData("/about/", "About", "Home", "Home")]
        [InlineData("/about/index", "About", "Home", "Home")]
        [InlineData("/about/privacy", "About", "Privacy", "Privacy")]
        [InlineData("/policies/about/privacy", "About", "Privacy", "Privacy")]
        [InlineData("/policies/about/pr'ivacy", "About", "Pr Ivacy", "Pr Ivacy")]
        public void ReturnsPageName(string path, string expectedCategory, string expectedPageName, string expectedTitle)
        {
            var result = ((PathString)path).GetPageInfo();

            Assert.Equal((expectedCategory, expectedPageName, expectedTitle), (result.Category, result.Name, result.Title));
        }
    }
}
