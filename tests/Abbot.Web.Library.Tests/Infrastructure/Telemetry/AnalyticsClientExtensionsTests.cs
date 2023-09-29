using System.Collections.Generic;
using Abbot.Common.TestHelpers.Fakes;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Models;
using Xunit;
using Organization = Serious.Abbot.Entities.Organization;

public class AnalyticsClientExtensionsTests
{
    public class TheTrackMethod
    {
        [Fact]
        public void TracksMemberAndOrganizationWithDefaultProperties()
        {
            var actor = new Member { Id = 42 };
            var organization = new Organization { Id = 23, PlanType = PlanType.Business };
            var client = new FakeAnalyticsClient();

            client.Track("Some Event", AnalyticsFeature.Conversations, actor, organization);

            client.AssertTracked(
                "Some Event",
                AnalyticsFeature.Conversations,
                actor,
                organization);
        }

        [Fact]
        public void TracksMemberAndOrganizationAndCombinesProperties()
        {
            var actor = new Member { Id = 42 };
            var organization = new Organization { Id = 23, PlanType = PlanType.Business };
            var properties = new Dictionary<string, object?>
            {
                {"foo", "bar"}
            };
            var client = new FakeAnalyticsClient();

            client.Track("Some Event", AnalyticsFeature.Conversations, actor, organization, properties);

            client.AssertTracked(
                "Some Event",
                AnalyticsFeature.Conversations,
                actor,
                organization,
                new {
                    foo = "bar"
                });
        }
    }

    public class TheScreenMethod
    {
        [Fact]
        public void TracksMemberAndOrganizationAndCombinesProperties()
        {
            var actor = new Member { Id = 42 };
            var organization = new Organization { Id = 23, PlanType = PlanType.Business };
            var properties = new Dictionary<string, object?>
            {
                {"foo", "bar"}
            };
            var client = new FakeAnalyticsClient();

            client.Screen(
                "Some Screen",
                AnalyticsFeature.AppHome,
                actor,
                organization,
                properties);

            client.AssertScreen(
                "Some Screen",
                AnalyticsFeature.AppHome,
                actor,
                organization,
                new {
                    foo = "bar"
                });
        }
    }
}
