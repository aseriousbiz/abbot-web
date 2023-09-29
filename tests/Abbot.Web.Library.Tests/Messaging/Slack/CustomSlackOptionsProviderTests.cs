using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Messaging.Slack;
using Serious.Abbot.Repositories;
using Serious.Slack.AspNetCore;
using Serious.TestHelpers;
using Xunit;

namespace Abbot.Web.Library.Tests.Messaging.Slack;

public class CustomSlackOptionsProviderTests
{
    public class TheGetOptionsAsyncMethod
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReturnsDefaultOptionsForRequestWithoutIntegrationId(bool enabled)
        {
            var integrations = Substitute.For<IIntegrationRepository>();
            var options = new SlackOptions { SlackSignatureValidationEnabled = enabled };

            var httpContext = new FakeHttpContext();

            var provider = new CustomSlackOptionsProvider(integrations, Options.Create(options));

            var result = await provider.GetOptionsAsync(httpContext);

            Assert.Same(options, result);
        }

        [Fact]
        public async Task ReturnsDefaultOptionsForRequestWithIntegrationIdButValidationDisabled()
        {
            var integrations = Substitute.For<IIntegrationRepository>();
            var options = new SlackOptions { SlackSignatureValidationEnabled = false };

            var httpContext = new FakeHttpContext();
            httpContext.Request.QueryString = new QueryString($"?integrationId=");

            var provider = new CustomSlackOptionsProvider(integrations, Options.Create(options));

            var result = await provider.GetOptionsAsync(httpContext);

            Assert.Same(options, result);
        }

        [Theory]
        [InlineData("", null)]
        [InlineData(".", null)]
        [InlineData("0", null)] // Not found
        [InlineData("1", null)] // Not Slack
        [InlineData("2", null)] // Slack without secret
        [InlineData("3", "ss")] // Slack with secret
        public async Task ReturnsOptionsFromIntegrationId(string id, string? expectedSigningSecret)
        {
            var integrations = Substitute.For<IIntegrationRepository>();
            integrations.GetIntegrationByIdAsync(1).Returns(new Integration { });

            var withoutCredentials = new Integration { Type = IntegrationType.SlackApp };
            integrations.GetIntegrationByIdAsync(2).Returns(withoutCredentials);
            integrations.ReadSettings<SlackAppSettings>(withoutCredentials).Returns(new SlackAppSettings());

            var withCredentials = new Integration { Type = IntegrationType.SlackApp };
            integrations.GetIntegrationByIdAsync(3).Returns(withCredentials);
            integrations.ReadSettings<SlackAppSettings>(withCredentials)
                .Returns(new SlackAppSettings
                {
                    Credentials = new()
                    {
                        SigningSecret = new Serious.Cryptography.SecretString("ss", new FakeDataProtectionProvider()),
                    },
                });

            var options = new SlackOptions { SlackSignatureValidationEnabled = true };

            var httpContext = new FakeHttpContext();
            httpContext.Request.QueryString = new QueryString($"?integrationId={id}");

            var provider = new CustomSlackOptionsProvider(integrations, Options.Create(options));

            var result = await provider.GetOptionsAsync(httpContext);

            Assert.NotSame(options, result);
            Assert.Equal(expectedSigningSecret, result.SigningSecret);
        }
    }
}
