using System.Collections.Generic;
using Serious.Abbot.Runtime;
using Xunit;

public class RequestHeadersTests
{
    public class TheIndexerProperty
    {
        [Fact]
        public void GetsKeysCaseInsensitively()
        {
            var values = new Dictionary<string, string[]>
            {
                {"Webhook-Request-Origin", new[] {"https://localhost/"}},
                {"accept", new[] {"application/json"}}
            };

            var headers = new RequestHeaders(values);

            Assert.Equal("https://localhost/", headers["Webhook-Request-Origin"]);
            Assert.Equal("https://localhost/", headers["WEBHOOK-Request-Origin"]);
            Assert.Equal("application/json", headers["accept"]);
            Assert.Equal("application/json", headers["ACCEPT"]);
        }
    }

    public class TheOriginProperty
    {
        [Fact]
        public void GetsOriginHeader()
        {
            var values = new Dictionary<string, string[]>
            {
                {"Origin", new[] {"example.com"}}
            };

            var headers = new RequestHeaders(values);

            Assert.Equal("example.com", headers.Origin);
        }

        [Fact]
        public void IsEmptyWhenHeaderDoesNotExist()
        {
            var headers = new RequestHeaders(new Dictionary<string, string[]>());

            var value = headers.Origin.ToString();
            Assert.NotNull(value);
            Assert.Empty(value);
        }
    }

    public class TheAcceptProperty
    {
        [Fact]
        public void GetsAcceptHeader()
        {
            var values = new Dictionary<string, string[]>
            {
                {"Accept", new[] {"application/xml", "application/json"}}
            };

            var headers = new RequestHeaders(values);

            Assert.Equal("application/xml", headers.Accept[0]);
            Assert.Equal("application/json", headers.Accept[1]);
        }

        [Fact]
        public void IsEmptyWhenHeaderDoesNotExist()
        {
            var headers = new RequestHeaders(new Dictionary<string, string[]>());

            var value = headers.Origin.ToString();
            Assert.NotNull(value);
            Assert.Empty(value);
        }
    }

    public class TheRefererProperty
    {
        [Fact]
        public void GetsRefererHeader()
        {
            var values = new Dictionary<string, string[]>
            {
                {"Referer", new[] {"https://example.com"}}
            };

            var headers = new RequestHeaders(values);

            Assert.Equal("https://example.com", headers.Referrer);
        }

        [Fact]
        public void IsEmptyWhenHeaderDoesNotExist()
        {
            var headers = new RequestHeaders(new Dictionary<string, string[]>());

            var value = headers.Referrer.ToString();
            Assert.NotNull(value);
            Assert.Empty(value);
        }
    }

    public class TheUserAgentProperty
    {
        [Fact]
        public void GetsUserAgentHeader()
        {
            var values = new Dictionary<string, string[]>
            {
                {"User-Agent", new[] {"Mozilla (fake)"}}
            };

            var headers = new RequestHeaders(values);

            Assert.Equal("Mozilla (fake)", headers.UserAgent);
        }

        [Fact]
        public void IsEmptyWhenHeaderDoesNotExist()
        {
            var headers = new RequestHeaders(new Dictionary<string, string[]>());

            var value = headers.UserAgent.ToString();
            Assert.NotNull(value);
            Assert.Empty(value);
        }
    }

    public class TheWebHookRequestOriginProperty
    {
        [Fact]
        public void GetsWebHookRequestOriginHeader()
        {
            var values = new Dictionary<string, string[]>
            {
                {"WebHook-Request-Origin", new[] {"events.example.com"}}
            };

            var headers = new RequestHeaders(values);

            Assert.Equal("events.example.com", headers.WebHookRequestOrigin);
        }

        [Fact]
        public void CanImplicitlyCastToString()
        {
            var values = new Dictionary<string, string[]>
            {
                {"WebHook-Request-Origin", new[] {"events.example.com"}}
            };
            var headers = new RequestHeaders(values);

            string? origin = headers.WebHookRequestOrigin;

            Assert.Equal("events.example.com", origin);
        }

        [Fact]
        public void IsEmptyWhenHeaderDoesNotExist()
        {
            var headers = new RequestHeaders(new Dictionary<string, string[]>());

            var value = headers.WebHookRequestOrigin.ToString();
            Assert.NotNull(value);
            Assert.Empty(value);
        }
    }

    public class TheWebHookRequestCallbackProperty
    {
        [Fact]
        public void GetsWebHookRequestCallbackHeader()
        {
            var values = new Dictionary<string, string[]>
            {
                {"WebHook-Request-Callback", new[] {"https://example.com/foo/bar"}}
            };

            var headers = new RequestHeaders(values);

            Assert.Equal("https://example.com/foo/bar", headers.WebHookRequestCallback);
        }

        [Fact]
        public void IsEmptyWhenHeaderDoesNotExist()
        {
            var headers = new RequestHeaders(new Dictionary<string, string[]>());

            var callback = headers.WebHookRequestCallback.ToString();
            Assert.NotNull(callback);
            Assert.Empty(callback);
        }
    }

    public class TheWebHookRequestRateProperty
    {
        [Fact]
        public void GetsWebHookRequestRateHeader()
        {
            var values = new Dictionary<string, string[]>
            {
                {"WebHook-Request-Rate", new[] {"42"}}
            };

            var headers = new RequestHeaders(values);

            Assert.Equal(42, headers.WebHookRequestRate);
        }

        [Fact]
        public void IsNullWhenHeaderNotExist()
        {
            var headers = new RequestHeaders(new Dictionary<string, string[]>());

            Assert.Null(headers.WebHookRequestRate);
        }

        [Fact]
        public void IsNullWhenHeaderInvalid()
        {
            var values = new Dictionary<string, string[]>
            {
                {"WebHook-Request-Rate", new[] {"abracadabra"}}
            };

            var headers = new RequestHeaders(values);

            Assert.Null(headers.WebHookRequestRate);
        }
    }
}
