using System.Collections.Generic;
using Serious.Abbot.Events;
using Xunit;

public class PrivateMetadataTests
{
    public class TheToStringMethod
    {
        [Fact]
        public void ReturnsFormattedPrivateMetadata()
        {
            var metadata = new PrivateMetadata("foo", "bar");
            Assert.Equal("foo|bar", metadata.ToString());
        }

        [Fact]
        public void ReturnsThreePartPrivateMetadata()
        {
            var metadata = new PrivateMetadataWithOptionalThird("foo", "bar", "baz");
            Assert.Equal("foo|bar|baz", metadata.ToString());
        }

        [Fact]
        public void ReturnsThreePartPrivateMetadataWithNullValue()
        {
            var metadata = new PrivateMetadataWithOptionalThird("foo", "bar");
            Assert.Equal("foo|bar|", metadata.ToString());
        }
    }

    public class TheTrySplitPartsMethod
    {
        [Fact]
        public void CanParseThreePartMetadata()
        {
            var privateMetadata = "foo|bar|baz";

            var result = PrivateMetadataWithOptionalThird.Parse(privateMetadata);

            Assert.NotNull(result);
            var (one, two, three) = result;
            Assert.Equal(("foo", "bar", "baz"), (one, two, three));
        }

        [Fact]
        public void CanParseTwoPartMetadata()
        {
            var privateMetadata = "foo|bar|";

            var result = PrivateMetadataWithOptionalThird.Parse(privateMetadata);

            Assert.NotNull(result);
            var (one, two, three) = result;
            Assert.Equal(("foo", "bar", string.Empty), (one, two, three));
        }
    }

    public class TheImplicitStringConversion
    {
        [Fact]
        public void ReturnsFormattedPrivateMetadata()
        {
            var metadata = new PrivateMetadata("foo", "bar");
            Assert.Equal("foo|bar", metadata);
        }
    }

    record PrivateMetadata(string One, string Two) : PrivateMetadataBase
    {
        protected override IEnumerable<string> GetValues()
        {
            yield return One;
            yield return Two;
        }

        public override string ToString() => base.ToString();
    }

    record PrivateMetadataWithOptionalThird(string One, string Two, string? Three = null) : PrivateMetadataBase
    {
        public static PrivateMetadataWithOptionalThird? Parse(string? privateMetadata)
        {
            return TrySplitParts(privateMetadata, 3, out var parts)
                ? new PrivateMetadataWithOptionalThird(parts[0], parts[1], parts[2])
                : null;
        }

        protected override IEnumerable<string> GetValues()
        {
            yield return One;
            yield return Two;
            yield return Three ?? string.Empty;
        }

        public override string ToString() => base.ToString();
    }
}
