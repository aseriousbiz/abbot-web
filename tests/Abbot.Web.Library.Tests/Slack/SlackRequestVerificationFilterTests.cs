using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Serious.Slack.AspNetCore;
using Serious.TestHelpers;
using Xunit;

public class SlackRequestVerificationFilterTests
{
    public class TheOnAuthorizationAsyncMethod
    {
        [Fact]
        public async Task AuthorizesValidSlackRequestAndDoesNotDisposeRequestStream()
        {
            var httpContext = new FakeHttpContext();
            var options = new FakeSlackOptionsProvider(httpContext);

            var filter = new SlackRequestVerificationFilter(options, NullLogger<SlackRequestVerificationFilter>.Instance)
            {
                Now = DateTimeOffset.FromUnixTimeSeconds(1616207883)
            };

            httpContext.Request.Headers["X-Slack-Request-Timestamp"] = "1616207883";
            httpContext.Request.Headers["X-Slack-Signature"] = "v0=F5888A3019DFB7F67C9EFB481BAC08D00B737054912905E9CBF63D0B119589BC";

            var stream = new MemoryStream();
            await stream.WriteStringAsync("{\"payload\"{}}");
            httpContext.Request.Body = stream;
            var actionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = new List<object>
                {
                    new VerifySlackRequestAttribute()
                }
            };

            var context = new FakeAuthorizationFilterContext(httpContext, actionDescriptor);

            await filter.OnAuthorizationAsync(context);

            Assert.Null(context.Result);
            var requestContent = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
            Assert.Equal("{\"payload\"{}}", requestContent);
            Assert.Equal("{\"payload\"{}}", httpContext.Items["Slack:RequestBody"]);
        }

        [Theory]
        [InlineData("1616207883", null)]
        [InlineData(null, "v0=F5888A3019DFB7F67C9EFB481BAC08D00B737054912905E9CBF63D0B119589BC")]
        public async Task ReturnsAcceptedWhenTimeStampOrSignatureMissing(string ts, string signature)
        {
            var httpContext = new FakeHttpContext();
            var options = new FakeSlackOptionsProvider(httpContext);

            var filter = new SlackRequestVerificationFilter(options, NullLogger<SlackRequestVerificationFilter>.Instance)
            {
                Now = DateTimeOffset.FromUnixTimeSeconds(1616207883)
            };

            httpContext.Request.Headers["X-Slack-Request-Timestamp"] = ts;
            httpContext.Request.Headers["X-Slack-Signature"] = signature;
            var actionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = new List<object>
                {
                    new VerifySlackRequestAttribute()
                }
            };

            var context = new FakeAuthorizationFilterContext(httpContext, actionDescriptor);

            await filter.OnAuthorizationAsync(context);

            Assert.IsType<AcceptedResult>(context.Result);
        }

        [Fact]
        public async Task ReturnsAcceptedWhenSignatureDoesNotMatch()
        {
            var httpContext = new FakeHttpContext();
            var options = new FakeSlackOptionsProvider(httpContext);

            var filter = new SlackRequestVerificationFilter(options, NullLogger<SlackRequestVerificationFilter>.Instance)
            {
                Now = DateTimeOffset.FromUnixTimeSeconds(1616207883)
            };

            httpContext.Request.Headers["X-Slack-Request-Timestamp"] = "1616207883";
            httpContext.Request.Headers["X-Slack-Signature"] = "v0=F5888A3019DFB7F67C9EFB481BAC08D00B737054912905E9CBF63D0B119589BD";

            var actionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = new List<object>
                {
                    new VerifySlackRequestAttribute()
                }
            };

            var context = new FakeAuthorizationFilterContext(httpContext, actionDescriptor);

            await filter.OnAuthorizationAsync(context);

            Assert.IsType<AcceptedResult>(context.Result);
        }

        [Fact]
        public async Task IgnoresActionsThatDoNotHaveVerifySlackRequestAttribute()
        {
            var httpContext = new FakeHttpContext();
            var options = new FakeSlackOptionsProvider(httpContext);

            var filter = new SlackRequestVerificationFilter(options, NullLogger<SlackRequestVerificationFilter>.Instance)
            {
                Now = DateTimeOffset.FromUnixTimeSeconds(1616207883)
            };

            httpContext.Request.Headers["X-Slack-Request-Timestamp"] = "1616207883";
            httpContext.Request.Headers["X-Slack-Signature"] = "v0=F5888A3019DFB7F67C9EFB481BAC08D00B737054912905E9CBF63D0B119589BD";

            var actionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = new List<object>()
            };

            var context = new FakeAuthorizationFilterContext(httpContext, actionDescriptor);

            await filter.OnAuthorizationAsync(context);

            Assert.Null(context.Result);
        }

        [Fact]
        public async Task IgnoresEverythingWhenVerificationNotEnabled()
        {
            var httpContext = new FakeHttpContext();
            var options = new FakeSlackOptionsProvider(httpContext, enabled: false, signingSecret: null);

            var filter = new SlackRequestVerificationFilter(options, NullLogger<SlackRequestVerificationFilter>.Instance)
            {
                Now = DateTimeOffset.FromUnixTimeSeconds(1616207883)
            };

            httpContext.Request.Headers["X-Slack-Request-Timestamp"] = "1616207883";
            httpContext.Request.Headers["X-Slack-Signature"] = "v0=BOGUS";

            var actionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = new List<object>
                {
                    new VerifySlackRequestAttribute()
                }
            };

            var context = new FakeAuthorizationFilterContext(httpContext, actionDescriptor);

            await filter.OnAuthorizationAsync(context);

            Assert.Null(context.Result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task ReturnsAcceptedWhenVerificationEnabledButSigningSecretNotFound(string? signingSecret)
        {
            // This is our problem, not Slack's
            var httpContext = new FakeHttpContext();
            var options = new FakeSlackOptionsProvider(httpContext, enabled: true, signingSecret: signingSecret);

            var filter = new SlackRequestVerificationFilter(options, NullLogger<SlackRequestVerificationFilter>.Instance)
            {
                Now = DateTimeOffset.FromUnixTimeSeconds(1616207883)
            };

            httpContext.Request.Headers["X-Slack-Request-Timestamp"] = "1616207883";
            httpContext.Request.Headers["X-Slack-Signature"] = "v0=BOGUS";

            var actionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = new List<object>
                {
                    new VerifySlackRequestAttribute()
                }
            };

            var context = new FakeAuthorizationFilterContext(httpContext, actionDescriptor);

            await filter.OnAuthorizationAsync(context);

            Assert.IsType<AcceptedResult>(context.Result);
        }

        [Fact]
        public async Task ReturnsConflictWhenTimeStampMoreThanFiveMinutesOld()
        {
            var httpContext = new FakeHttpContext();
            var options = new FakeSlackOptionsProvider(httpContext);

            var filter = new SlackRequestVerificationFilter(options, NullLogger<SlackRequestVerificationFilter>.Instance)
            {
                Now = DateTimeOffset.FromUnixTimeSeconds(1616207883).AddMinutes(6)
            };

            httpContext.Request.Headers["X-Slack-Request-Timestamp"] = "1616207883";
            httpContext.Request.Headers["X-Slack-Signature"] = "v0=F5888A3019DFB7F67C9EFB481BAC08D00B737054912905E9CBF63D0B119589BC";

            var stream = new MemoryStream();
            await stream.WriteStringAsync("{\"payload\"{}}");
            httpContext.Request.Body = stream;
            var actionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = new List<object>
                {
                    new VerifySlackRequestAttribute()
                }
            };

            var context = new FakeAuthorizationFilterContext(httpContext, actionDescriptor);

            await filter.OnAuthorizationAsync(context);

            Assert.IsType<ConflictResult>(context.Result);
        }

        private class FakeSlackOptionsProvider : ISlackOptionsProvider
        {
            public FakeSlackOptionsProvider(HttpContext httpContext, string? signingSecret = "signingSecret", bool enabled = true)
            {
                _options = new SlackOptions
                {
                    SigningSecret = signingSecret,
                    SlackSignatureValidationEnabled = enabled,
                };
                _httpContext = httpContext;
            }

            private readonly SlackOptions _options;
            readonly HttpContext _httpContext;

            public Task<SlackOptions> GetOptionsAsync(HttpContext httpContext)
            {
                Assert.Same(_httpContext, httpContext);
                return Task.FromResult(_options);
            }
        }
    }
}
