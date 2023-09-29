using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.Extensions.Options;
using Serious;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Slack.BlockKit;
using Serious.TestHelpers;
using Xunit;

public class HubSpotWebhookSignatureVerificationFilterTests
{
    public class TheOnAuthorizationAsyncMethod
    {
        [Fact]
        public async Task AuthorizesValidHubSpotWebhookRequestAndDoesNotDisposeRequestStream()
        {
            var httpContext = new FakeHttpContext();
            var clock = new TimeTravelClock();
            clock.TravelTo(new DateTime(year: 2022, month: 12, day: 7, hour: 3, minute: 30, second: 0, DateTimeKind.Utc));
            var options = Options.Create(
                new HubSpotOptions
                {
                    ClientSecret = "abbababa-beef-aaaa-bbbb-abbaabbaabba"
                });
            httpContext.Request.Method = "POST";
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("abbot-haacked-dev.ngrok.io");
            httpContext.Request.Path = "/hubspot/webhook";
            httpContext.Request.Headers["X-HubSpot-Request-Timestamp"] = "1670383777720";
            httpContext.Request.Headers["X-HubSpot-Signature-v3"] = "qXo1PASfXKd8KLy3T8tuwuZ2nmvAanXW+UjN3w+b4cg=";
            var stream = new MemoryStream();
            const string requestBody = """[{"eventId":3814323381,"subscriptionId":1870311,"portalId":22761544,"appId":1098266,"occurredAt":1670383777358,"subscriptionType":"conversation.newMessage","attemptNumber":0,"objectId":3624100517,"messageId":"e835c5f42631462ea4447601c8e1e394","messageType":"MESSAGE","changeFlag":"NEW_MESSAGE"}]""";
            await stream.WriteStringAsync(requestBody);
            httpContext.Request.Body = stream;
            var actionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = new List<object>
                {
                    new VerifyHubSpotRequestAttribute()
                }
            };
            var context = new FakeAuthorizationFilterContext(httpContext, actionDescriptor);
            var filter = new HubSpotWebhookSignatureVerificationFilter(options, clock);

            await filter.OnAuthorizationAsync(context);

            Assert.Null(context.Result);
            var requestContent = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
            Assert.Equal(requestBody, requestContent);
            Assert.Equal(requestBody, httpContext.Items["HubSpot:RequestBody"]);
        }

        [Theory]
        [InlineData("1616207883", null)]
        [InlineData(null, "qXo1PASfXKd8KLy3T8tuwuZ2nmvAanXW+UjN3w+b4cg=")]
        public async Task ReturnsAcceptedWhenTimeStampOrSignatureMissing(string ts, string signature)
        {
            var httpContext = new FakeHttpContext();
            var clock = new TimeTravelClock();
            clock.TravelTo(new DateTime(year: 2022, month: 12, day: 7, hour: 3, minute: 30, second: 0, DateTimeKind.Utc));
            var options = Options.Create(
                new HubSpotOptions
                {
                    ClientSecret = "abbababa-beef-aaaa-bbbb-abbaabbaabba"
                });
            httpContext.Request.Method = "POST";
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("abbot-haacked-dev.ngrok.io");
            httpContext.Request.Path = "/hubspot/webhook";
            httpContext.Request.Headers["X-HubSpot-Request-Timestamp"] = ts;
            httpContext.Request.Headers["X-HubSpot-Signature-v3"] = signature;
            var stream = new MemoryStream();
            const string requestBody = """[{"eventId":3814323381,"subscriptionId":1870311,"portalId":22761544,"appId":1098266,"occurredAt":1670383777358,"subscriptionType":"conversation.newMessage","attemptNumber":0,"objectId":3624100517,"messageId":"e835c5f42631462ea4447601c8e1e394","messageType":"MESSAGE","changeFlag":"NEW_MESSAGE"}]""";
            await stream.WriteStringAsync(requestBody);
            httpContext.Request.Body = stream;
            var actionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = new List<object>
                {
                    new VerifyHubSpotRequestAttribute()
                }
            };
            var context = new FakeAuthorizationFilterContext(httpContext, actionDescriptor);
            var filter = new HubSpotWebhookSignatureVerificationFilter(options, clock);

            await filter.OnAuthorizationAsync(context);

            Assert.IsType<AcceptedResult>(context.Result);
        }

        [Fact]
        public async Task ReturnsAcceptedWhenSignatureDoesNotMatch()
        {
            var httpContext = new FakeHttpContext();
            var clock = new TimeTravelClock();
            clock.TravelTo(new DateTime(year: 2022, month: 12, day: 7, hour: 3, minute: 30, second: 0, DateTimeKind.Utc));
            var options = Options.Create(
                new HubSpotOptions
                {
                    ClientSecret = "abbababa-beef-aaaa-bbbb-abbaabbaabba"
                });
            httpContext.Request.Method = "POST";
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("abbot-haacked-dev.ngrok.io");
            httpContext.Request.Path = "/hubspot/webhook";
            httpContext.Request.Headers["X-HubSpot-Request-Timestamp"] = "1670383777720";
            httpContext.Request.Headers["X-HubSpot-Signature-v3"] = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX=";
            var stream = new MemoryStream();
            const string requestBody = """[{"eventId":3814323381,"subscriptionId":1870311,"portalId":22761544,"appId":1098266,"occurredAt":1670383777358,"subscriptionType":"conversation.newMessage","attemptNumber":0,"objectId":3624100517,"messageId":"e835c5f42631462ea4447601c8e1e394","messageType":"MESSAGE","changeFlag":"NEW_MESSAGE"}]""";
            await stream.WriteStringAsync(requestBody);
            httpContext.Request.Body = stream;
            var actionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = new List<object>
                {
                    new VerifyHubSpotRequestAttribute()
                }
            };
            var context = new FakeAuthorizationFilterContext(httpContext, actionDescriptor);
            var filter = new HubSpotWebhookSignatureVerificationFilter(options, clock);

            await filter.OnAuthorizationAsync(context);

            Assert.IsType<AcceptedResult>(context.Result);
        }

        [Fact]
        public async Task IgnoresActionsThatDoNotHaveVerifyHubSpotRequestAttributeAttribute()
        {
            var httpContext = new FakeHttpContext();
            var clock = new TimeTravelClock();
            clock.TravelTo(new DateTime(year: 2022, month: 12, day: 7, hour: 3, minute: 30, second: 0, DateTimeKind.Utc));
            var options = Options.Create(
                new HubSpotOptions
                {
                    ClientSecret = "abbababa-beef-aaaa-bbbb-abbaabbaabba"
                });
            httpContext.Request.Method = "POST";
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("abbot-haacked-dev.ngrok.io");
            httpContext.Request.Path = "/hubspot/webhook";
            httpContext.Request.Headers["X-HubSpot-Request-Timestamp"] = "1670383777720";
            httpContext.Request.Headers["X-HubSpot-Signature-v3"] = "qXo1PASfXKd8KLy3T8tuwuZ2nmvAanXW+UjN3w+b4cg=";
            var stream = new MemoryStream();
            const string requestBody = """[{"eventId":3814323381,"subscriptionId":1870311,"portalId":22761544,"appId":1098266,"occurredAt":1670383777358,"subscriptionType":"conversation.newMessage","attemptNumber":0,"objectId":3624100517,"messageId":"e835c5f42631462ea4447601c8e1e394","messageType":"MESSAGE","changeFlag":"NEW_MESSAGE"}]""";
            await stream.WriteStringAsync(requestBody);
            httpContext.Request.Body = stream;
            var actionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = new List<object>()
            };
            var context = new FakeAuthorizationFilterContext(httpContext, actionDescriptor);
            var filter = new HubSpotWebhookSignatureVerificationFilter(options, clock);

            await filter.OnAuthorizationAsync(context);

            Assert.Null(context.Result);
        }

        [Fact]
        public async Task ReturnsConflictWhenTimeStampMoreThanFiveMinutesOld()
        {
            var httpContext = new FakeHttpContext();
            var clock = new TimeTravelClock();
            clock.TravelTo(new DateTime(year: 2022, month: 12, day: 7, hour: 3, minute: 45, second: 0, DateTimeKind.Utc));
            var options = Options.Create(
                new HubSpotOptions
                {
                    ClientSecret = "abbababa-beef-aaaa-bbbb-abbaabbaabba"
                });
            httpContext.Request.Method = "POST";
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("abbot-haacked-dev.ngrok.io");
            httpContext.Request.Path = "/hubspot/webhook";
            httpContext.Request.Headers["X-HubSpot-Request-Timestamp"] = "1670383777720";
            httpContext.Request.Headers["X-HubSpot-Signature-v3"] = "qXo1PASfXKd8KLy3T8tuwuZ2nmvAanXW+UjN3w+b4cg=";
            var stream = new MemoryStream();
            const string requestBody = """[{"eventId":3814323381,"subscriptionId":1870311,"portalId":22761544,"appId":1098266,"occurredAt":1670383777358,"subscriptionType":"conversation.newMessage","attemptNumber":0,"objectId":3624100517,"messageId":"e835c5f42631462ea4447601c8e1e394","messageType":"MESSAGE","changeFlag":"NEW_MESSAGE"}]""";
            await stream.WriteStringAsync(requestBody);
            httpContext.Request.Body = stream;
            var actionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = new List<object>
                {
                    new VerifyHubSpotRequestAttribute()
                }
            };
            var context = new FakeAuthorizationFilterContext(httpContext, actionDescriptor);
            var filter = new HubSpotWebhookSignatureVerificationFilter(options, clock);

            await filter.OnAuthorizationAsync(context);

            Assert.IsType<ConflictResult>(context.Result);
        }
    }
}
