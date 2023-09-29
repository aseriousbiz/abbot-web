using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Scripting;
using Serious.Cryptography;
using Serious.TestHelpers;

namespace Serious.Abbot.Messaging;

public class UnitTestTurnContextTranslator : ITurnContextTranslator
{
    Organization? _organization;

    public void SetOrganization(Organization organization)
    {
        _organization = organization;
    }

    public async Task<IPlatformMessage?> TranslateMessageAsync(ITurnContext turnContext)
    {
        return await Task.FromResult(new UnitTestPlatformMessage(
            _organization ?? new Organization { PlatformBotUserId = "abbot", PlatformId = "T0123456789" },
            turnContext,
            BotFromChannelAccount));
    }

    public Task<InstallEvent> TranslateInstallEventAsync(ITurnContext turnContext)
    {
        var installEvent = new InstallEvent(
            "T001",
            PlatformType.UnitTest,
            "B001",
            "abbot",
            "Unit Test",
            "unit-test",
            new SecretString("Token123", new FakeDataProtectionProvider()),
            null,
            BotAvatar: "https://exapmle.com/avatar.png",
            Avatar: "https://example.com/org-avatar.png");
        return Task.FromResult(installEvent);
    }

    public Task<IPlatformEvent?> TranslateUninstallEventAsync(ITurnContext turnContext)
    {
        var orgEvent = new PlatformEvent<UninstallPayload>(
            new UninstallPayload("platformId", "A001"),
            null,
            new BotChannelUser("T001", "U001", "Abbot"),
            DateTimeOffset.UtcNow,
            new FakeResponder(),
            new Member(),
            null,
            new Organization
            {
                PlatformId = "platformId",
                PlatformType = PlatformType.UnitTest
            });

        return Task.FromResult<IPlatformEvent?>(orgEvent);
    }

    public Task<IPlatformEvent?> TranslateEventAsync(ITurnContext turnContext)
    {
        return Task.FromResult<IPlatformEvent?>(null);
    }

    static BotChannelUser BotFromChannelAccount(string platformId, ChannelAccount channelAccount)
    {
        return new BotChannelUser(platformId, (string?)channelAccount.Id, (string?)channelAccount.Name);
    }
}
