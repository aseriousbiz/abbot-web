using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serious;
using Serious.Abbot.Controllers;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Repositories;
using Xunit;

public class HubSpotWebhookControllerTests : ControllerTestBase<HubSpotWebhookController>
{
    public class TheType
    {
        [Fact]
        public void HasVerifyHubSpotRequestAttributeApplied()
        {
            var result = typeof(HubSpotWebhookController).HasAttribute<VerifyHubSpotRequestAttribute>();
            Assert.True(result);
        }
    }

    public class TheWebhookAsyncMethod : HubSpotWebhookControllerTests
    {
        [Fact]
        public async Task EnqueuesNewMessagePayloads()
        {
            var payloads = new HubSpotWebhookPayload[]
            {
                new (1234567, "change", 913842384, 123412341234, 123421342123, 123412341234, 12341345213, "conversation.newMessage", 0) { MessageId = "b0a710adb0a710ad", MessageType = "MESSAGE" },
                new (1512512, "change", 987982349, 345109803440, 923421342123, 123412341234, 12341345216, "ticket.newTicket", 0),
            };
            var payloadsJson = JsonConvert.SerializeObject(payloads);

            await InvokeControllerAsync<ContentResult>(c => {
                c.HttpContext.Items[VerifyHubSpotRequestAttribute.RequestBodyKey] = payloadsJson;
                return c.WebhookAsync();
            });

            var synchronizationSetting = await Env.Settings.GetAsync(SettingsScope.HubSpotPortal(123421342123),
                "HubSpotMessageImport:1234567:b0a710adb0a710ad");
            Assert.NotNull(synchronizationSetting);

            Env.BackgroundJobClient.DidEnqueue<HubSpotToSlackImporter>(
                i => i.ImportMessageAsync(
                    synchronizationSetting.Id,
                    "b0a710adb0a710ad",
                    1234567L,
                    123421342123L));
        }

        [Fact]
        public async Task DoesNotEnqueueSameMessageTwice()
        {
            var firstPayloads = new HubSpotWebhookPayload[]
            {
                new (1234567, "change", 913842384, 123412341234, 123421342123, 123412341234, 12341345213, "conversation.newMessage", 0) { MessageId = "b0a710adb0a710ad", MessageType = "MESSAGE" },
            };
            var payloadsJson = JsonConvert.SerializeObject(firstPayloads);
            await InvokeControllerAsync<ContentResult>(c => {
                c.HttpContext.Items[VerifyHubSpotRequestAttribute.RequestBodyKey] = payloadsJson;
                return c.WebhookAsync();
            });
            Env.Db.ThrowUniqueConstraintViolationOnSave("Settings", "IX_Settings_Scope_Name");
            var retryPayloads = new[]
            {
                firstPayloads[0] with { AttemptNumber = 1 },
            };
            var retryPayloadsJson = JsonConvert.SerializeObject(retryPayloads);
            await InvokeControllerAsync<ContentResult>(c => {
                c.HttpContext.Items[VerifyHubSpotRequestAttribute.RequestBodyKey] = retryPayloadsJson;
                return c.WebhookAsync(); // We don't throw because we don't want HubSpot to retry.
            });

            var synchronizationSetting = await Env.Settings.GetAsync(SettingsScope.HubSpotPortal(123421342123),
                "HubSpotMessageImport:1234567:b0a710adb0a710ad");
            Assert.NotNull(synchronizationSetting);

            Env.BackgroundJobClient.DidEnqueue<HubSpotToSlackImporter>(
                i => i.ImportMessageAsync(
                    synchronizationSetting.Id,
                    "b0a710adb0a710ad",
                    1234567L,
                    123421342123L));
        }
    }
}
