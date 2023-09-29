using Serious.Abbot.Components;
using Xunit;

public class BreadCrumbItemTests
{
    public class TheActiveProperty
    {
        [Theory]
        [InlineData("/foo", "/foo", true)]
        [InlineData("/foo", "/foo/", true)]
        [InlineData("/foo/bar", "/foo", false)]
        [InlineData("/foo/", "/foo/bar", false)]
        public void ReturnsTrueWhenCurrentMatchesHref(
            string href,
            string path,
            bool expected)
        {
            var item = new BreadCrumbItem(href, path);

            Assert.Equal(expected, item.Active);
        }
    }

    public class TheNameProperty
    {
        [Theory]
        [InlineData("/foo", "Foo")]
        [InlineData("/foo/", "Foo")]
        [InlineData("/foo/bar", "Bar")]
        [InlineData("/foo/bar/", "Bar")]
        public void ReturnsCapitalizedName(
            string href,
            string expected)
        {
            var item = new BreadCrumbItem(href, "/");

            Assert.Equal(expected, item.Name);
        }
    }

    public class TheParentProperty
    {
        [Theory]
        [InlineData("/foo", null)]
        [InlineData("/foo/bar", "/foo")]
        public void ReturnsParentPathOfCurrentItem(
            string href,
            string expected)
        {
            var item = new BreadCrumbItem(href, "/baz");

            var parent = item.Parent;

            Assert.Equal(expected, parent?.Href);
        }
    }

    public class TheHrefProperty
    {
        [Theory]
        [InlineData("/foo", "/foo")]
        [InlineData("/foo/bar", "/foo/bar")]
        [InlineData("/baz", "#")]
        public void ReturnsHrefExceptWhenCurrentPage(
            string href,
            string expected)
        {
            var item = new BreadCrumbItem(href, "/baz");

            Assert.Equal(expected, item.Href);
        }
    }
}
