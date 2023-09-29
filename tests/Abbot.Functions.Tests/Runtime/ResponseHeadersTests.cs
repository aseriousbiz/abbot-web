using System;
using Serious.Abbot.Runtime;
using Xunit;

public class ResponseHeadersTests
{
    public class TheWebHookAllowedOriginProperty
    {
        [Fact]
        public void GetsWebHookAllowedOriginHeader()
        {
            var headers = new ResponseHeaders
            {
                ["WebHook-Allowed-Origin"] = "ab.bot"
            };

            var result = headers.WebHookAllowedOrigin;

            Assert.Equal("ab.bot", result);
        }

        [Fact]
        public void SetsWebHookAllowedOriginHeader()
        {
            var headers = new ResponseHeaders();

            headers.WebHookAllowedOrigin = "haacked.com";

            Assert.Equal("haacked.com", headers["WebHook-Allowed-Origin"]);
        }
    }

    public class TheWebHookAllowedRateProperty
    {
        [Fact]
        public void ReturnsZeroIfNotSet()
        {
            var headers = new ResponseHeaders();

            var result = headers.WebHookAllowedRate;

            Assert.Equal(0, result);
        }

        [Fact]
        public void ReturnsWebHookAllowedRateHeader()
        {
            var headers = new ResponseHeaders
            {
                ["WebHook-Allowed-Rate"] = "23"
            };

            var result = headers.WebHookAllowedRate;

            Assert.Equal(23, result);
        }

        [Fact]
        public void SetsWebHookAllowedRateHeader()
        {
            var headers = new ResponseHeaders();

            headers.WebHookAllowedRate = 42;

            Assert.Equal("42", headers["WebHook-Allowed-Rate"]);
        }

        [Fact]
        public void ThrowsExceptionWhenRateIsOutOfRange()
        {
            var headers = new ResponseHeaders();

            Assert.Throws<ArgumentOutOfRangeException>(() => headers.WebHookAllowedRate = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => headers.WebHookAllowedRate = 121);
        }
    }
}
