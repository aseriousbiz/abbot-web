using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.Cryptography;
using Serious.Slack.AspNetCore;
using Serious.TestHelpers;
using Xunit;

public class EntityExtensionsTests
{
    public class TheGetFirstRespondersMethod
    {
        [Fact]
        public void DoesNotIncludeInactiveMembers()
        {
            var room = new Room
            {
                Assignments = new[]
                {
                    new RoomAssignment
                    {
                        Member = new Member
                        {
                            Id = 1,
                            Active = true
                        },
                        Role = RoomRole.FirstResponder
                    },
                    new RoomAssignment
                    {
                        Member = new Member
                        {
                            Id = 2,
                            Active = false
                        },
                        Role = RoomRole.FirstResponder
                    },
                    new RoomAssignment
                    {
                        Member = new Member
                        {
                            Id = 3,
                            Active = true
                        },
                        Role = RoomRole.EscalationResponder
                    },
                }
            };

            var assignees = room.GetFirstResponders();

            var responder = Assert.Single(assignees);
            Assert.Equal(1, responder.Id);
        }
    }

    public class TheGetEscalationRespondersMethod
    {
        [Fact]
        public void DoesNotIncludeInactiveMembers()
        {
            var room = new Room
            {
                Assignments = new[]
                {
                    new RoomAssignment { Member = new Member { Id = 1, Active = true }, Role = RoomRole.FirstResponder },
                    new RoomAssignment { Member = new Member { Id = 2, Active = false }, Role = RoomRole.FirstResponder },
                    new RoomAssignment { Member = new Member { Id = 3, Active = true }, Role = RoomRole.EscalationResponder },
                    new RoomAssignment { Member = new Member { Id = 4, Active = false }, Role = RoomRole.EscalationResponder },
                }
            };

            var assignees = room.GetEscalationResponders();

            var responder = Assert.Single(assignees);
            Assert.Equal(3, responder.Id);
        }
    }

    public class TheIsCompleteMethod
    {
        [Theory]
        [InlineData(null, null, Organization.DefaultAvatar, PlanType.Unlimited, false)]
        [InlineData("name", null, Organization.DefaultAvatar, PlanType.Unlimited, false)]
        [InlineData(null, null, "https://localhost/img.jpg", PlanType.Unlimited, false)]
        [InlineData("name", null, "https://localhost/img.jpg", PlanType.Unlimited, false)]
        [InlineData(null, "domain", Organization.DefaultAvatar, PlanType.Unlimited, false)]
        [InlineData(null, "domain", "https://localhost/img.jpg", PlanType.Unlimited, false)]
        [InlineData("name", "domain", Organization.DefaultAvatar, PlanType.Unlimited, false)]
        [InlineData("name", "domain", "https://localhost/img.jpg", PlanType.None, false)]
        [InlineData("name", "domain", "https://localhost/img.jpg", PlanType.Unlimited, true)]
        public void ReturnsTrueOnlyIfNameDomainAvatarAndPlanAreNotDefaults(
            string name, string domain, string avatar, PlanType plan, bool expected)
        {
            var organization = new Organization
            {
                Name = name,
                Domain = domain,
                Avatar = avatar,
                PlanType = plan,
            };

            var result = organization.IsComplete();

            Assert.Equal(expected, result);
        }
    }

    public class TheGetSlackAppConfigurationUrlMethod
    {
        [Theory]
        [InlineData(null, "A0123", "Test")]
        [InlineData("test.slack.com", null, "Test")]
        [InlineData("test.slack.com", "A0123", null)]
        public void ReturnsNullForMissingDomainOrBotAppIdOrBotAppName(string? domain, string? appId, string? appName)
        {
            var organization = new Organization
            {
                Domain = domain,
                BotAppId = appId,
                BotAppName = appName,
            };

            Assert.Null(organization.GetSlackAppConfigurationUrl(new SlackOptions()));
        }

        [Theory]
        [InlineData("A0123", "Keith's Fancy App #1", "A0123-keiths-fancy-app-1")]
        [InlineData("A0123", " - Dashify ! Spaces - ", "A0123--dashify-spaces-")]
        public void SanitizesAppName(string? appId, string? appName, string expectedSlug)
        {
            var organization = new Organization
            {
                Domain = "test.slack.com",
                BotAppId = appId,
                BotAppName = appName,
            };

            var url = organization.GetSlackAppConfigurationUrl(new SlackOptions());

            Assert.Equal(new Uri($"https://test.slack.com/apps/{expectedSlug}?tab=settings"), url);
        }

        [Fact]
        public void ReplacesTokensInAppConfigurationUrl()
        {
            var slackOptions = new SlackOptions
            {
                AppConfigurationUrl = "https://ab.bot/{DOMAIN}/{APP_ID}/{BOT_APP_NAME}",
            };

            var organization = new Organization
            {
                Domain = "test.slack.com",
                BotAppId = "A0123",
                BotAppName = "Test",
            };

            var url = organization.GetSlackAppConfigurationUrl(slackOptions);

            Assert.Equal(new Uri("https://ab.bot/test.slack.com/A0123/test"), url);
        }
    }

    public class TheHasApiTokenMethod
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData("Token", true)]
        public void ReturnsTrueIfOrganizationHasAnApiToken(string? apiToken, bool expected)
        {
            var organization = new Organization
            {
                ApiToken = apiToken is null ? null : new SecretString(apiToken, new FakeDataProtectionProvider()),
            };

            var result = organization.HasApiToken();

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReturnsFalseIfSlackAuthorizationIsNull()
        {
            var auth = (SlackAuthorization?)null;

            var result = auth.HasApiToken();

            Assert.False(result);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("Token", true)]
        public void ReturnsTrueIfSlackAuthorizationHasAnApiToken(string? apiToken, bool expected)
        {
            var auth = new SlackAuthorization
            {
                ApiToken = apiToken is null ? null : new SecretString(apiToken, new FakeDataProtectionProvider()),
            };

            var result = auth.HasApiToken();

            Assert.Equal(expected, result);
        }
    }

    public class TheTryGetUnprotectedApiTokenMethod
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData("Token", true)]
        public void ReturnsTrueIfOrganizationHasAnApiToken(string? apiToken, bool expected)
        {
            var organization = new Organization
            {
                ApiToken = apiToken is null ? null : new SecretString(apiToken, new FakeDataProtectionProvider()),
            };

            var result = organization.TryGetUnprotectedApiToken(out var actualToken);

            Assert.Equal(expected, result);
            Assert.Equal(apiToken, actualToken);
        }

        [Fact]
        public void ReturnsFalseIfSlackAuthorizationIsNull()
        {
            var auth = (SlackAuthorization?)null;

            var result = auth.TryGetUnprotectedApiToken(out var actualToken);

            Assert.False(result);
            Assert.Null(actualToken);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("Token", true)]
        public void ReturnsTrueIfSlackAuthorizationHasAnApiToken(string? apiToken, bool expected)
        {
            var auth = new SlackAuthorization
            {
                ApiToken = apiToken is null ? null : new SecretString(apiToken, new FakeDataProtectionProvider()),
            };

            var result = auth.TryGetUnprotectedApiToken(out var actualToken);

            Assert.Equal(expected, result);
            Assert.Equal(apiToken, actualToken);
        }
    }

    public class TheDivergesFromPackageMethod
    {
        [Fact]
        public void ReturnsTrueIfSkillCodeDivergesFromSourcePackage()
        {
            var skill = new Skill
            {
                CacheKey = "unique",
                SourcePackageVersion = new PackageVersion
                {
                    Package = new Package
                    {
                        Versions = new List<PackageVersion>
                        {
                            new PackageVersion { CodeCacheKey = "not-unique" },
                            new PackageVersion { CodeCacheKey = "something-else" }
                        }
                    }
                }
            };

            Assert.True(skill.DivergesFromPackage());
        }

        [Fact]
        public void ReturnsFalseIfSkillCodeMatchesPreviousVersionOfSourcePackage()
        {
            var skill = new Skill
            {
                CacheKey = "unique",
                SourcePackageVersion = new PackageVersion
                {
                    Package = new Package
                    {
                        Versions = new List<PackageVersion>
                        {
                            new PackageVersion { CodeCacheKey = "not-unique" },
                            new PackageVersion { CodeCacheKey = "something-else" },
                            new PackageVersion { CodeCacheKey = "unique" }
                        }
                    }
                }
            };

            Assert.False(skill.DivergesFromPackage());
        }
    }

    public class TheHasUnpublishedChangesMethod
    {
        [Theory]
        [InlineData("Usage", "Description", "// new code", true)]
        [InlineData("Usage", "New Description", "// some code", true)]
        [InlineData("New Usage", "Description", "// some code", true)]
        [InlineData("Usage", "Description", "// some code", false)]
        public void ReturnsTrueIfSkillHasChanges(string usage, string description, string code, bool expected)
        {
            var package = new Package
            {
                UsageText = "Usage",
                Description = "Description",
                Code = "// some code"
            };
            var skill = new Skill
            {
                UsageText = usage,
                Description = description,
                Code = code,
                SourcePackageVersion = null
            };

            var result = skill.HasUnpublishedChanges(package);

            Assert.Equal(expected, result);
        }
    }

    public class TheCreateNextVersionMethod
    {
        [Theory]
        [InlineData(ChangeType.Patch, 1, 2, 4)]
        [InlineData(ChangeType.Minor, 1, 3, 0)]
        [InlineData(ChangeType.Major, 2, 0, 0)]
        public void IncrementsVersionsCorrectly(ChangeType changeType, int expectedMajor, int expectedMinor, int expectedPatch)
        {
            var current = new PackageVersion { MajorVersion = 1, MinorVersion = 2, PatchVersion = 3 };

            var result = current.CreateNextVersion(changeType);

            Assert.Equal(expectedMajor, result.MajorVersion);
            Assert.Equal(expectedMinor, result.MinorVersion);
            Assert.Equal(expectedPatch, result.PatchVersion);
        }
    }

    public class TheUpdateMemberInstanceFromUserEventMethod
    {
        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(true, false, true)]
        public void UpdatesUserAndMemberFromUserEventPayload(bool isBot, bool wasAbbot, bool isAbbot)
        {
            var user = new User
            {
                IsAbbot = wasAbbot,
                DisplayName = "some-user",
                PlatformUserId = "U0123456789"
            };
            var member = new Member
            {
                User = user,
                Active = true,
                Organization = new Organization
                {
                    PlatformId = "T08675309",
                    PlatformType = PlatformType.Slack,
                    PlatformBotUserId = isAbbot ? user.PlatformUserId : "U240",
                    PlanType = PlanType.Free
                }
            };
            var userEventPayload = new UserEventPayload(
                "U0123456789",
                "T08675309",
                "new-real-name",
                "new-display-name",
                "to@example.com",
                "America/Los_Angeles",
                "https://example.com/avatar.png",
                IsBot: isBot);

            member.UpdateMemberInstanceFromUserEventPayload(userEventPayload);

            Assert.Equal("new-display-name", member.DisplayName);
            Assert.Equal("America/Los_Angeles", member.TimeZoneId);
            Assert.Equal("new-display-name", user.DisplayName);
            Assert.Equal("new-real-name", user.RealName);
            Assert.Equal("to@example.com", user.Email);
            Assert.Equal("https://example.com/avatar.png", user.Avatar);
            Assert.Equal(isBot, user.IsBot);
            Assert.Equal(wasAbbot | isAbbot, user.IsAbbot);
            Assert.True(member.Active);
        }

        [Fact]
        public void DoesNotUpdateEmailIfOrgIsForeignOrg()
        {
            var user = new User
            {
                DisplayName = "some-user",
                PlatformUserId = "U0123456789"
            };
            var member = new Member
            {
                User = user,
                Active = true,
                Organization = new Organization
                {
                    PlatformId = "T08675309",
                    PlatformType = PlatformType.Slack,
                    PlanType = PlanType.None,
                }
            };
            var userEventPayload = new UserEventPayload(
                "U0123456789",
                "T08675309",
                "new-real-name",
                "new-display-name",
                "to@example.com",
                "America/Los_Angeles",
                "https://example.com/avatar.png");

            member.UpdateMemberInstanceFromUserEventPayload(userEventPayload);

            Assert.Equal("new-display-name", member.DisplayName);
            Assert.Equal("America/Los_Angeles", member.TimeZoneId);
            Assert.Equal("new-display-name", user.DisplayName);
            Assert.Equal("new-real-name", user.RealName);
            Assert.Null(user.Email);
            Assert.Equal("https://example.com/avatar.png", user.Avatar);
            Assert.True(member.Active);
        }

        [Fact]
        public void ClearsEmailIfUserIsGuestAccount()
        {
            var user = new User
            {
                DisplayName = "some-user",
                PlatformUserId = "U0123456789",
                Email = "secret@example.com",
            };
            var member = new Member
            {
                User = user,
                Active = true,
                Organization = new Organization
                {
                    PlatformId = "T08675309",
                    PlatformType = PlatformType.Slack,
                    PlanType = PlanType.Free
                },
            };
            var userEventPayload = new UserEventPayload(
                "U0123456789",
                "T08675309",
                "new-real-name",
                "new-display-name",
                "to@example.com",
                "America/Los_Angeles",
                "https://example.com/avatar.png",
                IsGuest: true);

            member.UpdateMemberInstanceFromUserEventPayload(userEventPayload);

            Assert.Equal("new-display-name", member.DisplayName);
            Assert.Equal("America/Los_Angeles", member.TimeZoneId);
            Assert.Equal("new-display-name", user.DisplayName);
            Assert.Equal("new-real-name", user.RealName);
            Assert.Null(user.Email);
            Assert.True(member.IsGuest);
            Assert.Equal("https://example.com/avatar.png", user.Avatar);
            Assert.True(member.Active);
        }

        [Theory]
        [InlineData("T01234567", null, null, null)]
        [InlineData("T01234567", null, "secret@example.com", "secret@example.com")]
        [InlineData("T01234567", null, "example@pulumi.com", "example@pulumi.com")]
        [InlineData("T01234567", "user@example.com", null, "user@example.com")]
        [InlineData("T01234567", "user@example.com", "secret@example.com", "secret@example.com")]
        [InlineData("T01234567", "user@example.com", "example@pulumi.com", "example@pulumi.com")]
        [InlineData("T4PBPMA8J", null, null, null)]
        [InlineData("T4PBPMA8J", null, "secret@example.com", null)]
        [InlineData("T4PBPMA8J", null, "example@pulumi.com", "example@pulumi.com")]
        [InlineData("T4PBPMA8J", null, "example@PuLuMi.cOm", "example@PuLuMi.cOm")]
        [InlineData("T4PBPMA8J", "user@example.com", null, null)]
        [InlineData("T4PBPMA8J", "user@example.com", "secret@example.com", null)]
        [InlineData("T4PBPMA8J", "user@example.com", "example@pulumi.com", "example@pulumi.com")]
        [InlineData("T4PBPMA8J", "user@example.com", "example@PuLuMi.cOm", "example@PuLuMi.cOm")]
        [InlineData("T4PBPMA8J", "user@pulumi.com", null, "user@pulumi.com")]
        [InlineData("T4PBPMA8J", "user@pulumi.com", "secret@example.com", null)]
        [InlineData("T4PBPMA8J", "user@pulumi.com", "example@pulumi.com", "example@pulumi.com")]
        [InlineData("T4PBPMA8J", "user@pulumi.com", "example@PuLuMi.cOm", "example@PuLuMi.cOm")]
        public void ClearsEmailIfUserEmailIsNotCanonicalDomain(string platformId, string? userEmail, string? eventEmail, string expectedEmail)
        {
            var user = new User
            {
                DisplayName = "some-user",
                PlatformUserId = "U0123456789",
                Email = userEmail,
            };
            var member = new Member
            {
                User = user,
                Active = true,
                Organization = new Organization
                {
                    PlatformId = platformId,
                    PlatformType = PlatformType.Slack,
                    PlanType = PlanType.Free
                },
            };
            var userEventPayload = new UserEventPayload(
                "U0123456789",
                platformId,
                "new-real-name",
                "new-display-name",
                eventEmail,
                "America/Los_Angeles",
                "https://example.com/avatar.png",
                IsGuest: false);

            member.UpdateMemberInstanceFromUserEventPayload(userEventPayload);

            Assert.Equal("new-display-name", member.DisplayName);
            Assert.Equal("America/Los_Angeles", member.TimeZoneId);
            Assert.Equal("new-display-name", user.DisplayName);
            Assert.Equal("new-real-name", user.RealName);
            Assert.Equal(expectedEmail, user.Email);
            Assert.False(member.IsGuest);
            Assert.Equal("https://example.com/avatar.png", user.Avatar);
            Assert.True(member.Active);
        }

        [Fact]
        public void SetsActiveToFalseWhenDeleted()
        {
            var user = new User
            {
                DisplayName = "some-user",
                PlatformUserId = "U0123456789"
            };
            var member = new Member
            {
                User = user,
                Active = true,
                Organization = new Organization { PlatformId = "T08675309", PlatformType = PlatformType.Slack }
            };
            var userEventPayload = new UserEventPayload(
                "U0123456789",
                "T08675309",
                "new-real-name",
                "new-display-name",
                Deleted: true);

            member.UpdateMemberInstanceFromUserEventPayload(userEventPayload);

            Assert.False(member.Active);
            Assert.Null(user.SlackTeamId);
        }
    }

    public class TheGetOrCreateMemberInstanceForOrganizationMethod
    {
        [Fact]
        public void AddsActiveMemberRecordForOrganizationIfItDoesNotExist()
        {
            var user = new User
            {
                Members = new List<Member>
                {
                    new()
                    {
                        OrganizationId = 1,
                        Organization = new Organization
                        {
                            Id = 1,
                            PlatformId = "T00000001"
                        }
                    }
                }
            };
            var organization = new Organization { Id = 2, PlatformId = "T08675309" };

            var (result, entityState) = user.GetOrCreateMemberInstanceForOrganization(organization);

            Assert.Equal(2, user.Members.Count);
            var member = Assert.Single(user.Members.Where(m => m.Organization.Id == organization.Id));
            Assert.Equal(member.Id, result.Id);
            Assert.Equal(EntityEnsureState.Creating, entityState);
            Assert.True(member.Active);
        }

        [Fact]
        public void ReturnsExistingMatchingMember()
        {
            var organization = new Organization { Id = 2, PlatformId = "T08675309" };
            var user = new User
            {
                Members = new List<Member>
                {
                    new()
                    {
                        Id = 42,
                        OrganizationId = organization.Id,
                        Organization = organization
                    }
                }
            };

            var (result, entityState) = user.GetOrCreateMemberInstanceForOrganization(organization);

            Assert.Equal(1, user.Members.Count);
            var member = Assert.Single(user.Members);
            Assert.Equal(member.Id, result.Id);
            Assert.Equal(EntityEnsureState.Existing, entityState);
            Assert.True(member.Active);
        }
    }

    public class TheIsActiveTrialPlanMethod
    {
        [Theory]
        [InlineData(1, true)]
        [InlineData(-1, false)]
        public void ReturnsTrueWhenTrialPlanNotExpired(int expiryDays, bool expectedResult)
        {
            var now = new DateTime(2022, 1, 23);
            var expiry = now.AddDays(expiryDays);
            var organization = new Organization
            {
                Trial = new TrialPlan(PlanType.Business, expiry)
            };

            var result = organization.IsActiveTrialPlan(now);

            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void ReturnsFalseWhenTrialNull()
        {
            var now = new DateTime(2022, 1, 23);
            var organization = new Organization();

            var result = organization.IsActiveTrialPlan(now);

            Assert.Equal(false, result);
        }
    }

    public class TheCanAddAgentMethod
    {
        [Theory]
        [InlineData(PlanType.Free)]
        [InlineData(PlanType.Beta)]
        [InlineData(PlanType.FoundingCustomer)]
        [InlineData(PlanType.Team)]
        [InlineData(PlanType.Unlimited)]
        public void IsTrueForLegacyAndBrandNewPlans(PlanType planType)
        {
            var now = new DateTime(2022, 1, 23);
            var organization = new Organization { PlanType = planType };

            var result = organization.CanAddAgent(99, now);

            Assert.True(result);
        }

        [Theory]
        [InlineData(20, 20, false)]
        [InlineData(21, 20, true)]
        public void IsTrueWhenPurchasedSeatsOutnumberAgentCount(int purchased, int agentCount, bool expected)
        {
            var now = new DateTime(2022, 1, 23);
            var organization = new Organization
            {
                PurchasedSeatCount = purchased
            };

            var result = organization.CanAddAgent(agentCount, now);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsTrueWhenAnActiveTrial()
        {
            var now = new DateTime(2022, 1, 23);
            var expiry = now.AddDays(1);
            var organization = new Organization
            {
                Trial = new TrialPlan(PlanType.Business, expiry),
                PurchasedSeatCount = 0,
            };

            var result = organization.CanAddAgent(2, now);

            Assert.True(result);
        }
    }
}
