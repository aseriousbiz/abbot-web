using System.Net;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Settings.Organization;
using Serious.Abbot.Pages.Settings.Rooms;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;

public static class RoomPageTests
{
    public class TheOnGetAsyncMethod : PageTestBase<RoomPage>
    {
        [Fact]
        public async Task ReturnsNotFoundIfNoRoomWithId()
        {
            await Env.CreateRoomAsync("C123ABC");
            var (_, result) = await InvokePageAsync(p => p.OnGetAsync(NonExistent.PlatformRoomId));
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task LoadsRoomIfFound()
        {
            var room = await Env.CreateRoomAsync("C123ABC", name: "the-danger-room");
            var (page, result) = await InvokePageAsync(p => p.OnGetAsync("C123ABC"));
            Assert.IsType<PageResult>(result);
            Assert.Equal("the-danger-room", page.Room.Name);
            Assert.Equal(room.Id, page.Room.Id);
        }

        [Theory]
        [InlineData(60, 120, 1, TimeUnits.Minutes, false, 2, TimeUnits.Minutes, false)]
        [InlineData(0, 30, null, TimeUnits.Minutes, false, 1, TimeUnits.Minutes, true)]
        [InlineData(null, 30, null, TimeUnits.Minutes, false, 1, TimeUnits.Minutes, true)]
        [InlineData(30, null, 1, TimeUnits.Minutes, true, null, TimeUnits.Minutes, false)]
        [InlineData(60 * 60, 2 * 60 * 60, 1, TimeUnits.Hours, false, 2, TimeUnits.Hours, false)]
        [InlineData(90 * 60, 150 * 60, 90, TimeUnits.Minutes, false, 150, TimeUnits.Minutes, false)]
        [InlineData(24 * 60 * 60, 48 * 60 * 60, 1, TimeUnits.Days, false, 2, TimeUnits.Days, false)]
        [InlineData(36 * 60 * 60, 60 * 60 * 60, 36, TimeUnits.Hours, false, 60, TimeUnits.Hours, false)]
        public async Task LoadsTargetAndDeadlineValuesCorrectly(int? targetSeconds, int? deadlineSeconds,
            int? expectedTargetValue, TimeUnits? expectedTargetUnits, bool expectedTargetRounding,
            int? expectedDeadlineValue, TimeUnits? expectedDeadlineUnits, bool expectedDeadlineRounding)
        {
            var room = await Env.CreateRoomAsync("C123ABC", name: "the-danger-room");
            room.TimeToRespond = new Threshold<TimeSpan>(
                targetSeconds is null
                    ? null
                    : TimeSpan.FromSeconds(targetSeconds.Value),
                deadlineSeconds is null
                    ? null
                    : TimeSpan.FromSeconds(deadlineSeconds.Value));

            await Db.SaveChangesAsync();

            var (page, result) = await InvokePageAsync(p => p.OnGetAsync("C123ABC"));

            Assert.IsType<PageResult>(result);
            Assert.Equal(expectedTargetValue, page.ResponseTimeSettings.TargetValue);
            Assert.Equal(expectedTargetUnits, page.ResponseTimeSettings.TargetUnits);
            Assert.Equal(expectedTargetRounding, page.ResponseTimeSettings.IsTargetRounded);
            Assert.Equal(expectedDeadlineValue, page.ResponseTimeSettings.DeadlineValue);
            Assert.Equal(expectedDeadlineUnits, page.ResponseTimeSettings.DeadlineUnits);
            Assert.Equal(expectedDeadlineRounding, page.ResponseTimeSettings.IsDeadlineRounded);
        }

        [Fact]
        public async Task LoadsFirstRespondersAndTimeCoverageCorrectly()
        {
            Env.Clock.TravelTo(new DateTime(2022, 4, 19, 12, 0, 0));

            var room = await Env.CreateRoomAsync("C123ABC", name: "the-danger-room");
            var member1 = await Env.CreateMemberInAgentRoleAsync();
            member1.TimeZoneId = "Europe/Amsterdam";
            member1.WorkingHours = new(new(8, 0), new(14, 0));
            var member2 = await Env.CreateMemberInAgentRoleAsync();
            member2.TimeZoneId = "America/Vancouver";
            member2.WorkingHours = new(new(10, 0), new(17, 0));
            var member3 = await Env.CreateMemberInAgentRoleAsync();
            member3.TimeZoneId = "America/Vancouver";
            member3.WorkingHours = new(new(16, 30), new(19, 0));

            await Env.Rooms.AssignMemberAsync(room, member1, RoomRole.FirstResponder, Env.TestData.Member);
            await Env.Rooms.AssignMemberAsync(room, member2, RoomRole.FirstResponder, Env.TestData.Member);
            await Env.Rooms.AssignMemberAsync(room, member3, RoomRole.FirstResponder, Env.TestData.Member);

            var (page, result) = await InvokePageAsync(p => p.OnGetAsync(room.PlatformRoomId));
            Assert.IsType<PageResult>(result);
            Assert.Equal(new WorkingHours[]
                {
                    new(new(0, 0), new(5, 0)), new(new(10, 0), new(19, 0)), new(new(23, 0), new(0, 0)),
                },
                page.FirstResponderCoverage.ToArray());
        }

        [Fact]
        public async Task LoadsZendeskOrganizationLinkIfPresent()
        {
            Env.Clock.Freeze();
            await Env.CreateIntegrationAsync(IntegrationType.Zendesk, enabled: true);
            var room = await Env.CreateRoomAsync("C123ABC", name: "the-danger-room");
            await Env.Rooms.CreateLinkAsync(room,
                RoomLinkType.ZendeskOrganization,
                "https://d3v-test.zendesk.com/api/v2/organizations/999.json",
                "The Derek Zoolander Center for Kids Who Can't Read Good",
                Env.TestData.Member,
                Env.Clock.UtcNow);

            var (page, _) = await InvokePageAsync(p => p.OnGetAsync(room.PlatformRoomId));

            Assert.Equal("The Derek Zoolander Center for Kids Who Can't Read Good",
                page.ZendeskRoomLink?.DisplayName);
            Assert.Equal("https://d3v-test.zendesk.com/api/v2/organizations/999.json",
                page.ZendeskRoomLink?.Link?.ToString());
        }

        [Theory]
        [InlineData(null, null, false, null)]
        [InlineData(null, true, false, true)]
        [InlineData(false, true, false, true)]
        [InlineData(true, true, true, true)]
        [InlineData(true, false, true, false)]
        public async Task LoadsTicketEmojiSettings(bool? orgSetting, bool? roomSetting, bool expectedOrgSetting,
            bool? expectedRoomSetting)
        {
            Env.Clock.Freeze();
            var room = await Env.CreateRoomAsync("C123ABC", name: "the-danger-room");

            if (orgSetting is not null)
            {
                await Env.Settings.SetAsync(
                    SettingsScope.Organization(Env.TestData.Organization),
                    ReactionHandler.AllowTicketReactionSettingName,
                    orgSetting.Value.ToString().ToLowerInvariant(),
                    Env.TestData.User);
            }

            if (roomSetting is not null)
            {
                await Env.Settings.SetAsync(
                    SettingsScope.Room(room),
                    ReactionHandler.AllowTicketReactionSettingName,
                    roomSetting.Value.ToString().ToLowerInvariant(),
                    Env.TestData.User);
            }

            var (page, _) = await InvokePageAsync(p => p.OnGetAsync(room.PlatformRoomId));

            Assert.Equal(expectedOrgSetting, page.OrganizationEmojiSetting);
            Assert.Equal(expectedRoomSetting, page.RoomEmojiSetting);
        }
    }

    public class TheOnPostAsyncMethod : PageTestBase<RoomPage>
    {
        [Fact]
        public async Task ReturnsNotFoundIfNoRoomWithId()
        {
            await Env.CreateRoomAsync("C123ABC");
            var (_, result) = await InvokePageAsync(p => p.OnPostAsync(NonExistent.PlatformRoomId));
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task RedirectsWithMessageIfConversationTrackingNotAvailableInOrganization()
        {
            Env.TestData.Organization.PlanType = PlanType.Free;
            await Env.Db.SaveChangesAsync();

            await Env.CreateRoomAsync("C123ABC");
            var (_, result) = await InvokePageAsync(p => p.OnPostAsync("C123ABC"), acceptsTurbo: true);
            Assert.NotNull(result);
            result.AssertTurboFlashMessage("You must upgrade your plan to use this feature.");
        }

        [Fact]
        public async Task ReturnsPageWithoutModificationsIfModelStateErrors()
        {
            var room = await Env.CreateRoomAsync("C123ABC");
            ModelState.AddModelError(nameof(ResponseTimeSettings.TargetValue), "Blah blah blah");
            var (_, result) = await InvokePageAsync(async p => {
                p.ResponseTimeSettings = new ResponseTimeSettings
                {
                    TargetValue = 1,
                    TargetUnits = TimeUnits.Days
                };

                return await p.OnPostAsync("C123ABC");
            }, acceptsTurbo: true);

            await Env.ReloadAsync(room);
            Assert.Null(room.TimeToRespond.Warning);
            result.AssertTurboPartialResult(new("response-times-editor"), "_ResponseTimesForm");
        }

        [Theory]
        [InlineData(1, TimeUnits.Days, 1, TimeUnits.Hours)]
        [InlineData(2, TimeUnits.Hours, 1, TimeUnits.Hours)]
        public async Task ReturnsErrorIfDeadlineIsNotAfterTarget(
            int targetValue,
            TimeUnits targetUnits,
            int deadlineValue,
            TimeUnits deadlineUnits)
        {
            var room = await Env.CreateRoomAsync("C123ABC");
            var (_, result) = await InvokePageAsync(async p => {
                p.ResponseTimeSettings = new ResponseTimeSettings
                {
                    UseCustomResponseTimes = true,
                    TargetValue = targetValue,
                    TargetUnits = targetUnits,
                    DeadlineValue = deadlineValue,
                    DeadlineUnits = deadlineUnits
                };

                return await p.OnPostAsync("C123ABC");
            }, acceptsTurbo: true);

            await Env.ReloadAsync(room);
            Assert.Null(room.TimeToRespond.Warning);
            Assert.Null(room.TimeToRespond.Deadline);
            Assert.NotNull(result);
            result.AssertTurboPartialResult(new("response-times-editor"), "_ResponseTimesForm");
            Assert.Equal("The deadline must be after the target.",
                ModelState["ResponseTimeSettings.DeadlineValue"]?.Errors.SingleOrDefault()?.ErrorMessage);
        }

        [Theory]
        [InlineData(0, TimeUnits.Days, 0, TimeUnits.Minutes, null, null)]
        [InlineData(null, TimeUnits.Days, null, TimeUnits.Minutes, null, null)]
        [InlineData(1, TimeUnits.Days, 47, TimeUnits.Hours, 24 * 60 * 60, 47 * 60 * 60)]
        [InlineData(2, TimeUnits.Hours, 150, TimeUnits.Minutes, 120 * 60, 150 * 60)]
        public async Task UpdatesThresholdValuesIfValid(int? targetValue, TimeUnits targetUnits, int? deadlineValue,
            TimeUnits deadlineUnits, int? expectedTargetSeconds, int? expectedDeadlineSeconds)
        {
            var room = await Env.CreateRoomAsync("C123ABC");
            room.TimeToRespond = new Threshold<TimeSpan>(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));
            await Db.SaveChangesAsync();

            var (_, result) = await InvokePageAsync(async p => {
                p.ResponseTimeSettings = new ResponseTimeSettings
                {
                    UseCustomResponseTimes = true,
                    TargetValue = targetValue,
                    TargetUnits = targetUnits,
                    DeadlineValue = deadlineValue,
                    DeadlineUnits = deadlineUnits
                };

                return await p.OnPostAsync("C123ABC");
            }, acceptsTurbo: true);

            await Env.ReloadAsync(room);
            Assert.Equal(expectedTargetSeconds, room.TimeToRespond.Warning?.TotalSeconds);
            Assert.Equal(expectedDeadlineSeconds, room.TimeToRespond.Deadline?.TotalSeconds);
            result.AssertTurboFlashMessage("Room settings updated!");
            result.AssertTurboPartialResult(new("response-times-editor"), "_ResponseTimesForm");
        }
    }

    public class TheOnGetAutocompleteZendeskOrganizationsMethod : PageTestBase<RoomPage>
    {
        [Fact]
        public async Task ReturnsErrorIfRoomNotFound()
        {
            await Env.CreateRoomAsync("C123ABC");
            var (_, result) = await InvokePageAsync<PartialViewResult>(p =>
                p.OnGetAutocompleteZendeskOrganizations(NonExistent.PlatformRoomId, "foo"));

            var model = Assert.IsType<ZendeskOrganizationListModel>(result.Model);
            Assert.Equal("Room not found.", model.ErrorMessage);
            Assert.Empty(model.Organizations);
        }

        [Fact]
        public async Task ReturnsErrorIfZendeskIntegrationNotEnabled()
        {
            var room = await Env.CreateRoomAsync();
            var (_, result) =
                await InvokePageAsync<PartialViewResult>(p =>
                    p.OnGetAutocompleteZendeskOrganizations(room.PlatformRoomId, "foo"));

            var model = Assert.IsType<ZendeskOrganizationListModel>(result.Model);
            Assert.Equal("Zendesk integration is not enabled.", model.ErrorMessage);
            Assert.Empty(model.Organizations);
        }

        [Fact]
        public async Task ReturnsErrorIfZendeskIntegrationMissingApiCredentials()
        {
            var room = await Env.CreateRoomAsync();
            await Env.Integrations.EnableAsync(Env.TestData.Organization,
                IntegrationType.Zendesk,
                Env.TestData.Member);

            var (_, result) =
                await InvokePageAsync<PartialViewResult>(p =>
                    p.OnGetAutocompleteZendeskOrganizations(room.PlatformRoomId, "foo"));

            var model = Assert.IsType<ZendeskOrganizationListModel>(result.Model);
            Assert.Equal("Zendesk integration is not enabled.", model.ErrorMessage);
            Assert.Empty(model.Organizations);
        }

        [Fact]
        public async Task ReturnsErrorIfNoQueryProvided()
        {
            var room = await Env.CreateRoomAsync();
            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization,
                IntegrationType.Zendesk,
                Env.TestData.Member);

            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = "d3v-test",
                    ApiToken = Env.Secret("api-token"),
                });

            var (_, result) =
                await InvokePageAsync<PartialViewResult>(p =>
                    p.OnGetAutocompleteZendeskOrganizations(room.PlatformRoomId, ""));

            var model = Assert.IsType<ZendeskOrganizationListModel>(result.Model);
            Assert.Equal("You didn't specify a search query.", model.ErrorMessage);
            Assert.Empty(model.Organizations);
        }

        [Fact]
        public async Task ReturnsErrorIfZendeskThrows()
        {
            var room = await Env.CreateRoomAsync();
            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization,
                IntegrationType.Zendesk,
                Env.TestData.Member);

            var settings = new ZendeskSettings()
            {
                Subdomain = "d3v-test",
                ApiToken = Env.Secret("api-token"),
            };

            await Env.Integrations.SaveSettingsAsync(integration, settings);

            var client = Env.ZendeskClientFactory.ClientFor(settings.Subdomain);
            client.ThrowOn(nameof(client.AutocompleteOrganizationsAsync),
                HttpStatusCode.Unauthorized,
                HttpMethod.Get,
                "",
                new object());

            var (_, result) =
                await InvokePageAsync<PartialViewResult>(p =>
                    p.OnGetAutocompleteZendeskOrganizations(room.PlatformRoomId, "foo"));

            var model = Assert.IsType<ZendeskOrganizationListModel>(result.Model);
            Assert.Equal("Unable to load organizations from Zendesk.", model.ErrorMessage);
            Assert.Empty(model.Organizations);
        }

        [Fact]
        public async Task ReturnsSearchResults()
        {
            var room = await Env.CreateRoomAsync();
            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization,
                IntegrationType.Zendesk,
                Env.TestData.Member);

            var settings = new ZendeskSettings()
            {
                Subdomain = "d3v-test",
                ApiToken = Env.Secret("api-token"),
            };

            await Env.Integrations.SaveSettingsAsync(integration, settings);

            var client = Env.ZendeskClientFactory.ClientFor(settings.Subdomain);
            client.Organizations["The Derek Zoolander Center for Kids Who Can't Read Good"] = new ZendeskOrganization()
            {
                Id = 1
            };

            client.Organizations["The Very Big Corporation of America"] = new ZendeskOrganization()
            {
                Id = 2
            };

            client.Organizations["The Estelle Leonard Talent Agency"] = new ZendeskOrganization()
            {
                Id = 3
            };

            client.Organizations["Viridian Dynamics"] = new ZendeskOrganization()
            {
                Id = 4
            };

            var (_, result) =
                await InvokePageAsync<PartialViewResult>(p =>
                    p.OnGetAutocompleteZendeskOrganizations(room.PlatformRoomId, "The"));

            var model = Assert.IsType<ZendeskOrganizationListModel>(result.Model);
            Assert.Null(model.ErrorMessage);
            Assert.Equal(new long[] { 1, 2, 3 }, model.Organizations.Select(o => o.Id).ToArray());
        }
    }

    public class TheOnPostConversationSettingsAsyncMethod : PageTestBase<RoomPage>
    {
        [Fact]
        public async Task ReturnsNotFoundIfNoRoomWithId()
        {
            await Env.CreateRoomAsync("C123ABC");
            var (_, result) =
                await InvokePageAsync(p => p.OnPostConversationSettingsAsync(NonExistent.PlatformRoomId, false, false));

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task RemovesRoomSettingIfUseOrganizationDefaultSpecified()
        {
            var room = await Env.CreateRoomAsync();
            await Env.Settings.SetAsync(SettingsScope.Room(room),
                ReactionHandler.AllowTicketReactionSettingName,
                "true",
                Env.TestData.User);

            var (_, result) = await InvokePageAsync(
                p => p.OnPostConversationSettingsAsync(room.PlatformRoomId, true, false),
                acceptsTurbo: true);

            result.AssertTurboFlashMessage("Reverted to organization defaults for 🎫 emoji reactions.");
            Assert.Null(await Env.Settings.GetAsync(SettingsScope.Room(room),
                ReactionHandler.AllowTicketReactionSettingName));
        }

        [Theory]
        [InlineData(true, "The 🎫 emoji reaction has been enabled for this room.")]
        [InlineData(false, "The 🎫 emoji reaction has been disabled for this room.")]
        public async Task SetsSettingIfUseOrganizationDefaultIsFalse(bool allowTicketReactions, string expectedStatusMessage)
        {
            var room = await Env.CreateRoomAsync();
            var (_, result) = await InvokePageAsync(
                p => p.OnPostConversationSettingsAsync(room.PlatformRoomId, false, allowTicketReactions),
                acceptsTurbo: true);

            result.AssertTurboFlashMessage(expectedStatusMessage);

            var setting = await Env.Settings.GetAsync(SettingsScope.Room(room),
                ReactionHandler.AllowTicketReactionSettingName);
            Assert.NotNull(setting);
            Assert.Equal(allowTicketReactions.ToString(), setting.Value);
        }
    }

    public class TheOnPostUnlinkZendeskOrganizationMethod : PageTestBase<RoomPage>
    {
        [Fact]
        public async Task ReturnsNotFoundIfNoRoomWithId()
        {
            await Env.CreateRoomAsync("C123ABC");
            var (_, result) =
                await InvokePageAsync(p => p.OnPostUnlinkZendeskOrganizationAsync(NonExistent.PlatformRoomId));

            result.AssertTurboFlashMessage("Unable to find the requested room.");
        }

        [Fact]
        public async Task ReturnsNoPermissionIfCannotManageConversations()
        {
            var room = await Env.CreateRoomAsync();
            var (_, result) =
                await InvokePageAsync(p => p.OnPostUnlinkZendeskOrganizationAsync(room.PlatformRoomId));

            result.AssertTurboFlashMessage("You do not have permission to manage room links.");
        }

        [Theory]
        [InlineData(Roles.Agent)]
        [InlineData(Roles.Administrator)]
        public async Task RemovesAllLinkedZendeskOrganizations(string role)
        {
            await Env.AddUserToRoleAsync(Env.TestData.Member, role);
            var room = await Env.CreateRoomAsync();
            await Env.Rooms.CreateLinkAsync(room,
                RoomLinkType.ZendeskOrganization,
                "https://d3v-test.zendesk.com/api/v2/organizations/999.json",
                "The Derek Zoolander Center for Kids Who Can't Read Good",
                Env.TestData.Member,
                Env.Clock.UtcNow);

            await Env.ReloadAsync(room);
            Assert.NotEmpty(room.Links);

            var (_, result) = await InvokePageAsync(
                p => p.OnPostUnlinkZendeskOrganizationAsync(room.PlatformRoomId),
                acceptsTurbo: true);

            result.AssertTurboFlashMessage("Removed Zendesk Organization link.");
            await Env.ReloadAsync(room);
            Assert.Empty(room.Links);
        }
    }

    public class TheOnPostSetZendeskOrganizationAsyncMethod : PageTestBase<RoomPage>
    {
        [Fact]
        public async Task ReturnsNotFoundIfNoRoomWithId()
        {
            await Env.CreateRoomAsync("C123ABC");
            var (_, result) = await InvokePageAsync(
                p => p.OnPostSetZendeskOrganizationAsync(
                    NonExistent.PlatformRoomId,
                    "https://d3v-test.zendesk.com/api/v2/organizations/998.json",
                    "Iron Bank of Braavos"),
                acceptsTurbo: true);

            result.AssertTurboFlashMessage("Unable to find the requested room.");
        }

        [Fact]
        public async Task ReturnsNoPermissionIfCannotManageConversations()
        {
            var room = await Env.CreateRoomAsync();
            var (_, result) = await InvokePageAsync(
                p => p.OnPostSetZendeskOrganizationAsync(
                    room.PlatformRoomId,
                    "https://d3v-test.zendesk.com/api/v2/organizations/998.json",
                    "Iron Bank of Braavos"),
                acceptsTurbo: true);

            result.AssertTurboFlashMessage("You do not have permission to manage room links.");
        }

        [Theory]
        [InlineData(Roles.Agent)]
        [InlineData(Roles.Administrator)]
        public async Task ReplacesAllExistingLinksWithProvidedLinkData(string role)
        {
            await Env.AddUserToRoleAsync(Env.TestData.Member, role);
            var room = await Env.CreateRoomAsync();
            await Env.Rooms.CreateLinkAsync(room,
                RoomLinkType.ZendeskOrganization,
                "https://d3v-test.zendesk.com/api/v2/organizations/999.json",
                "The Derek Zoolander Center for Kids Who Can't Read Good",
                Env.TestData.Member,
                Env.Clock.UtcNow);

            await Env.Rooms.CreateLinkAsync(room,
                RoomLinkType.ZendeskOrganization,
                "https://d3v-test.zendesk.com/api/v2/organizations/998.json",
                "Veridian Dynamics",
                Env.TestData.Member,
                Env.Clock.UtcNow);

            await Env.ReloadAsync(room);
            Assert.NotEmpty(room.Links);

            var (_, result) = await InvokePageAsync(
                p => p.OnPostSetZendeskOrganizationAsync(
                    room.PlatformRoomId,
                    "https://d3v-test.zendesk.com/api/v2/organizations/998.json",
                    "Iron Bank of Braavos"),
                acceptsTurbo: true);

            result.AssertTurboFlashMessage("Room successfully linked to Zendesk Organization 'Iron Bank of Braavos'");
            await Env.ReloadAsync(room);
            Assert.Collection(room.Links,
                l => {
                    Assert.Equal(RoomLinkType.ZendeskOrganization, l.LinkType);
                    Assert.Equal("https://d3v-test.zendesk.com/api/v2/organizations/998.json", l.ExternalId);
                    Assert.Equal("Iron Bank of Braavos", l.DisplayName);
                });
        }
    }

    public class TheOnPostAssignAsyncMethod : PageTestBase<RoomPage>
    {
        [Fact]
        public async Task ReturnsNotFoundIfNoRoomWithId()
        {
            await Env.CreateRoomAsync("C123ABC");
            var (_, result) = await InvokePageAsync(p =>
                p.OnPostAssignAsync(NonExistent.PlatformRoomId, TestMember, RoomRole.FirstResponder));

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnsTurboFlashElementWithMessageIfConversationTrackingNotAvailableInOrganization()
        {
            Env.TestData.Organization.PlanType = PlanType.Free;
            await Env.Db.SaveChangesAsync();

            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: false);
            var (_, result) =
                await InvokePageAsync(p => p.OnPostAssignAsync(room.PlatformRoomId, TestMember, RoomRole.FirstResponder), acceptsTurbo: true);

            Assert.NotNull(result);
            result.AssertTurboFlashMessage("You must upgrade your plan to use this feature.");
        }

        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfAbbotIsNotTrackingConversations()
        {
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: false);
            var (_, result) =
                await InvokePageAsync(p => p.OnPostAssignAsync(room.PlatformRoomId, TestMember, RoomRole.FirstResponder),
                acceptsTurbo: true);

            Assert.NotNull(result);
            result.AssertTurboFlashMessage("Abbot is not tracking conversations in this room.");
        }

        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfMemberNotFound()
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Agent, Env.TestData.Abbot);
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: true);
            var (_, result) =
                await InvokePageAsync(
                    p => p.OnPostAssignAsync(room.PlatformRoomId, NonExistent.MemberId, RoomRole.FirstResponder),
                    acceptsTurbo: true);

            Assert.NotNull(result);
            result.AssertTurboFlashMessage("Member not found.");
        }

        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfMemberAlreadyAssigned()
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Agent, Env.TestData.Abbot);
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: true);
            await Env.Rooms.AssignMemberAsync(room, TestMember, RoomRole.FirstResponder, TestMember);
            var (_, result) = await InvokePageAsync(
                p => p.OnPostAssignAsync(room.PlatformRoomId, TestMember, RoomRole.FirstResponder),
                acceptsTurbo: true);

            Assert.NotNull(result);
            result.AssertTurboFlashMessage($"{TestMember.DisplayName} is already a first responder in this room.");
        }

        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfMemberIsNotInAgentsRole()
        {
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: true);
            await Env.Rooms.AssignMemberAsync(room, TestMember, RoomRole.FirstResponder, TestMember);
            var (_, result) = await InvokePageAsync(
                p => p.OnPostAssignAsync(room.PlatformRoomId, TestMember, RoomRole.FirstResponder),
                acceptsTurbo: true);

            Assert.NotNull(result);
            result.AssertTurboFlashMessage($"{WebConstants.ErrorStatusPrefix}{TestMember.DisplayName} must log in to this site and be assigned the Agent role first, before being added as a first responder.");
        }

        [Theory]
        [InlineData(RoomRole.FirstResponder,
            RoomRole.EscalationResponder,
            "Assigned Holden as a first responder for this room.")]
        [InlineData(RoomRole.EscalationResponder,
            RoomRole.FirstResponder,
            "Assigned Holden as an escalation responder for this room.")]
        public async Task AssignsMemberAndReturnsTurboStreamResultWithMessageIfMemberNotAlreadyAssigned(
            RoomRole roomRole,
            RoomRole otherRoomRole,
            string expectedStatusMessage)
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Agent, Env.TestData.Abbot);
            Env.TestData.User.DisplayName = "Holden";
            await Env.Db.SaveChangesAsync();
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: true);
            Assert.False(room.Assignments.Any(a => a.MemberId == TestMember.Id && a.Role == roomRole));
            Assert.False(room.Assignments.Any(a => a.MemberId == TestMember.Id && a.Role == otherRoomRole));
            var (_, result) = await InvokePageAsync(
                p => p.OnPostAssignAsync(room.PlatformRoomId, TestMember, roomRole),
                acceptsTurbo: true);

            await Env.ReloadAsync(room);
            Assert.NotNull(result);
            result.AssertTurboFlashMessage(expectedStatusMessage);
            Assert.True(room.Assignments.Any(a => a.MemberId == TestMember.Id && a.Role == roomRole));
            Assert.False(room.Assignments.Any(a => a.MemberId == TestMember.Id && a.Role == otherRoomRole));
        }
    }

    public class TheOnPostUnassignAsyncMethod : PageTestBase<RoomPage>
    {
        [Fact]
        public async Task ReturnsNotFoundIfNoRoomWithId()
        {
            await Env.CreateRoomAsync("C123ABC");
            var (_, result) = await InvokePageAsync(p =>
                p.OnPostUnassignAsync(NonExistent.PlatformRoomId, TestMember, RoomRole.FirstResponder));

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfConversationTrackingNotAvailableInOrganization()
        {
            Env.TestData.Organization.PlanType = PlanType.Free;
            await Env.Db.SaveChangesAsync();

            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: false);
            var (_, result) = await InvokePageAsync(
                p => p.OnPostUnassignAsync(room.PlatformRoomId, TestMember, RoomRole.FirstResponder),
                acceptsTurbo: true);

            Assert.NotNull(result);
            result.AssertTurboFlashMessage("You must upgrade your plan to use this feature.");
        }

        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfAbbotIsNotTrackingConversations()
        {
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: false);
            var (_, result) = await InvokePageAsync(
                p => p.OnPostUnassignAsync(room.PlatformRoomId, TestMember, RoomRole.FirstResponder),
                acceptsTurbo: true);

            Assert.NotNull(result);
            result.AssertTurboFlashMessage("Abbot is not tracking conversations in this room.");
        }

        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfMemberNotFound()
        {
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: true);
            var (_, result) = await InvokePageAsync(
                p => p.OnPostUnassignAsync(room.PlatformRoomId, NonExistent.MemberId, RoomRole.FirstResponder),
                acceptsTurbo: true);

            Assert.NotNull(result);
            result.AssertTurboFlashMessage("Member not found.");
        }

        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfMemberNotAssigned()
        {
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: true);
            var (_, result) = await InvokePageAsync(
                p => p.OnPostUnassignAsync(room.PlatformRoomId, TestMember, RoomRole.FirstResponder),
                acceptsTurbo: true);

            Assert.NotNull(result);
            result.AssertTurboFlashMessage($"{TestMember.DisplayName} is not a first responder in this room.");
        }

        [Theory]
        [InlineData(RoomRole.FirstResponder,
            RoomRole.EscalationResponder,
            "Naomi is no longer a first responder for this room.")]
        [InlineData(RoomRole.EscalationResponder,
            RoomRole.FirstResponder,
            "Naomi is no longer an escalation responder for this room.")]
        public async Task RemovesMemberFromRoomRoleAndReturnsTurboFlashWithMessageIfMemberAssigned(
            RoomRole roomRole,
            RoomRole otherRoomRole,
            string expectedStatusMessage)
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Agent, Env.TestData.Abbot);
            Env.TestData.User.DisplayName = "Naomi";
            await Env.Db.SaveChangesAsync();
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: true);
            await Env.Rooms.AssignMemberAsync(room, TestMember, RoomRole.FirstResponder, TestMember);
            await Env.Rooms.AssignMemberAsync(room, TestMember, RoomRole.EscalationResponder, TestMember);
            await Env.ReloadAsync(room);
            Assert.True(room.Assignments.Any(a => a.MemberId == TestMember.Id && a.Role == roomRole));
            Assert.True(room.Assignments.Any(a => a.MemberId == TestMember.Id && a.Role == otherRoomRole));

            var (_, result) = await InvokePageAsync(
                p => p.OnPostUnassignAsync(room.PlatformRoomId, TestMember, roomRole),
                acceptsTurbo: true);

            await Env.ReloadAsync(room);
            Assert.NotNull(result);
            result.AssertTurboFlashMessage(expectedStatusMessage);
            Assert.False(room.Assignments.Any(a => a.MemberId == TestMember.Id && a.Role == roomRole));
            Assert.True(room.Assignments.Any(a => a.MemberId == TestMember.Id && a.Role == otherRoomRole));
        }
    }

    public class TheOnPostUntrackAsyncMethod : PageTestBase<RoomPage>
    {
        [Fact]
        public async Task ReturnsNotFoundIfNoRoomWithId()
        {
            await Env.CreateRoomAsync("C123ABC");
            var (_, result) = await InvokePageAsync(p => p.OnPostUntrackAsync(NonExistent.PlatformRoomId));
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfConversationTrackingNotAvailableInOrganization()
        {
            Env.TestData.Organization.PlanType = PlanType.Free;
            await Env.Db.SaveChangesAsync();

            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: false);
            var (_, result) = await InvokePageAsync(p => p.OnPostUntrackAsync(room.PlatformRoomId), acceptsTurbo: true);
            Assert.NotNull(result);
            result.AssertTurboFlashMessage("You must upgrade your plan to use this feature.");
        }

        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfAbbotIsNotTrackingConversations()
        {
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: false);
            var (_, result) = await InvokePageAsync(p => p.OnPostUntrackAsync(room.PlatformRoomId), acceptsTurbo: true);
            Assert.NotNull(result);
            result.AssertTurboFlashMessage("Abbot is not tracking conversations in this room.");
        }

        [Fact]
        public async Task DisablesConversationTrackingAndReturnsTurboFlashWithMessageIfAbbotIsCurrentlyTrackingConversations()
        {
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: true);
            Assert.True(room.ManagedConversationsEnabled);

            var (_, result) = await InvokePageAsync(p => p.OnPostUntrackAsync(room.PlatformRoomId), acceptsTurbo: true);

            await Env.ReloadAsync(room);
            Assert.NotNull(result);
            result.AssertTurboFlashMessage("Abbot is no longer tracking conversations in this room.");
            Assert.False(room.ManagedConversationsEnabled);
        }
    }

    public class TheOnPostTrackAsyncMethod : PageTestBase<RoomPage>
    {
        [Fact]
        public async Task ReturnsNotFoundIfNoRoomWithId()
        {
            await Env.CreateRoomAsync("C123ABC");
            var (_, result) = await InvokePageAsync(p => p.OnPostTrackAsync(NonExistent.PlatformRoomId));
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfConversationTrackingNotAvailableInOrganization()
        {
            Env.TestData.Organization.PlanType = PlanType.Free;
            await Env.Db.SaveChangesAsync();

            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: true);
            var (_, result) = await InvokePageAsync(p => p.OnPostTrackAsync(room.PlatformRoomId), acceptsTurbo: true);
            Assert.NotNull(result);
            result.AssertTurboFlashMessage("You must upgrade your plan to use this feature.");
        }

        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfAbbotIsNotTrackingConversations()
        {
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: true);
            var (_, result) = await InvokePageAsync(p => p.OnPostTrackAsync(room.PlatformRoomId), acceptsTurbo: true);
            Assert.NotNull(result);
            result.AssertTurboFlashMessage("Abbot is already tracking conversations in this room.");
        }

        [Fact]
        public async Task DisablesConversationTrackingAndReturnsTurboFlashWithMessageIfAbbotIsCurrentlyTrackingConversations()
        {
            var room = await Env.CreateRoomAsync("C123ABC", managedConversationsEnabled: false);
            Assert.False(room.ManagedConversationsEnabled);

            var (_, result) = await InvokePageAsync(p => p.OnPostTrackAsync(room.PlatformRoomId), acceptsTurbo: true);

            await Env.ReloadAsync(room);
            Assert.NotNull(result);
            result.AssertTurboFlashMessage("Abbot is now tracking conversations in this room.");
            Assert.True(room.ManagedConversationsEnabled);
        }
    }
}
