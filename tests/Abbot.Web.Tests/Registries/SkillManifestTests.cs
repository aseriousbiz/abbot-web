using Abbot.Common.TestHelpers;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Messaging;
using Serious.Abbot.Metadata;
using Serious.Abbot.Models;
using Serious.Abbot.Services;
using Serious.Abbot.Skills;
using Serious.TestHelpers;

public class SkillManifestTests
{
    public class TheGetAllSkillDescriptorsAsyncMethod
    {
        [Fact]
        public async Task ReturnsAllDescriptorsExceptHiddenBuiltInSkillsAndDisabledSkills()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            await env.CreateSkillAsync("disabled", enabled: false);
            await env.CreateSkillAsync("userskill1");
            await env.CreateSkillAsync("userskill2");
            await env.CreateListAsync("deepthought");
            await env.CreateAliasAsync("deep", "userskill1", string.Empty);
            env.BuiltinSkillRegistry.AddSkills(
                new PingSkill(),
                new EchoSkill(),
                new FailSkill() /* Hidden */);
            var manifest = env.Activate<SkillManifest>();

            var descriptors = await manifest.GetAllSkillDescriptorsAsync(organization, new FakeFeatureActor());

            var expected = new[]
            {
                "ping", "echo", "userskill1", "userskill2", "joke", "deepthought", "remember", "deep"
            };
            Assert.Equal(expected, descriptors.Select(d => d.Name).ToArray());
        }

        [Theory]
        [InlineData(PlanType.Free, new[] { "ping", "echo" })]
        [InlineData(PlanType.Team, new[] { "ping", "echo" })]
        [InlineData(PlanType.Business, new[] { "ping", PlanFeatureTestSkill.Name, "echo" })]
        [InlineData(PlanType.FoundingCustomer, new[] { "ping", "echo" })]
        [InlineData(PlanType.Beta, new[] { "ping", PlanFeatureTestSkill.Name, "echo" })]
        [InlineData(PlanType.Unlimited, new[] { "ping", PlanFeatureTestSkill.Name, "echo" })]
        public async Task HidesBuiltInSkillsConditionedOnUnavailablePlanFeature(PlanType planType, string[] expectedDescriptors)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.PlanType = planType;
            await env.Db.SaveChangesAsync();
            env.BuiltinSkillRegistry.AddSkills(
                new PingSkill(),
                new PlanFeatureTestSkill(),
                new EchoSkill());
            var actor = new FakeFeatureActor("USER", "GROUP1", "GROUP2");

            var manifest = env.Activate<SkillManifest>();

            var descriptors = (await manifest
                .GetAllSkillDescriptorsAsync(organization, actor))
                .OfType<BuiltinSkillDescriptor>(); // Creating an org creates other skills we want to ignore for this test.

            Assert.Equal(expectedDescriptors, descriptors.Select(d => d.Name).ToArray());
        }

        [Theory]
        [InlineData(new string[0], new[] { "ping", "echo" })]
        [InlineData(new[] { "USER" }, new[] { "ping", FeatureFlagTestSkill.Name, "echo" })]
        [InlineData(new[] { "GROUP1" }, new[] { "ping", FeatureFlagTestSkill.Name, "echo" })]
        [InlineData(new[] { "USER", "GROUP2", "GROUP3" }, new[] { "ping", FeatureFlagTestSkill.Name, "echo" })]
        [InlineData(new[] { "GROUP3" }, new[] { "ping", "echo" })]
        public async Task HidesBuiltInSkillsConditionedOnDisabledFeatureFlags(string[] featureEnabledForGroups, string[] expectedDescriptors)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.BuiltinSkillRegistry.AddSkills(
                new PingSkill(),
                new FeatureFlagTestSkill(),
                new EchoSkill());
            var actor = new FakeFeatureActor("USER", "GROUP1", "GROUP2");
            // Disable the feature globally and enable it only for the users passed in
            env.Features.Set(FeatureFlagTestSkill.FlagName, false);
            foreach (var featureEnabled in featureEnabledForGroups)
            {
                env.Features.Set(FeatureFlagTestSkill.FlagName, featureEnabled, true);
            }

            var manifest = env.Activate<SkillManifest>();

            var descriptors = (await manifest
                .GetAllSkillDescriptorsAsync(organization, actor))
                .OfType<BuiltinSkillDescriptor>(); // Creating an org creates other skills we want to ignore for this test.

            Assert.Equal(expectedDescriptors, descriptors.Select(d => d.Name).ToArray());
        }
    }

    public class TheResolveSkillAsyncAsyncMethod
    {
        [Fact]
        public async Task ReturnsNullWhenNoSkillsExist()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;

            var manifest = env.Activate<SkillManifest>();

            var descriptor = await manifest
                .ResolveSkillAsync("anything", organization, new FakeFeatureActor());

            Assert.Null(descriptor);
        }

        [Fact]
        public async Task ReturnsNullForNonExistentSkill()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.BuiltinSkillRegistry.AddSkills(
                new PingSkill(),
                new EchoSkill());
            await env.CreateSkillAsync("userskill1");
            await env.CreateSkillAsync("userskill2");
            await env.CreateAliasAsync("deep", "userskill1", string.Empty);
            var manifest = env.Activate<SkillManifest>();

            var descriptor = await manifest
                .ResolveSkillAsync("not-found", organization, new FakeFeatureActor());

            Assert.Null(descriptor);
        }

        [Fact]
        public async Task ReturnsDescriptorInPriorityOrderWhenConflictsExist()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            await env.CreateSkillAsync("ping", description: "user defined ping");
            await env.CreateSkillAsync("userskill", description: "user defined userskill");
            await env.CreateListAsync("ping");
            await env.CreateListAsync("deepthought");
            await env.CreateAliasAsync("deepthought", "ping", string.Empty);
            await env.CreateAliasAsync("ping", "userskill1", string.Empty);
            await env.CreateAliasAsync("userskill", "userskill1", string.Empty);
            await env.CreateAliasAsync("alias1", "ping", string.Empty);
            env.BuiltinSkillRegistry.AddSkills(
                new PingSkill(),
                env.Activate<RemoteSkillCallSkill>(),
                env.Activate<ListSkill>());
            var manifest = env.Activate<SkillManifest>();

            var pingSkill = await manifest.ResolveSkillAsync(
                "ping",
                organization, new FakeFeatureActor());
            var userDefinedSkill = await manifest.ResolveSkillAsync(
                "userskill",
                organization, new FakeFeatureActor());
            var listSkillDescriptor = await manifest.ResolveSkillAsync(
                "deepthought",
                organization, new FakeFeatureActor());
            var aliasItem = await manifest.ResolveSkillAsync(
                "alias1",
                organization, new FakeFeatureActor());

            Assert.NotNull(pingSkill);
            Assert.IsType<PingSkill>(pingSkill.Skill);
            Assert.NotNull(userDefinedSkill);
            Assert.IsType<RemoteSkillCallSkill>(userDefinedSkill.Skill);
            Assert.NotNull(listSkillDescriptor);
            Assert.IsType<ListSkill>(listSkillDescriptor.Skill);
            Assert.NotNull(aliasItem);
            Assert.IsType<PingSkill>(aliasItem.Skill);
        }

        [Fact]
        public async Task ResolvesTheActualBuiltInSkillToCallForAnAlias()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.BuiltinSkillRegistry.AddSkill(new PingSkill());
            await env.CreateAliasAsync("pong", "ping", "return", description: "Pongs the ping");
            var manifest = env.Activate<SkillManifest>();

            var result = await manifest.ResolveSkillAsync(
                "pong",
                organization, new FakeFeatureActor());

            Assert.NotNull(result);
            Assert.Equal("ping", result.Name);
            Assert.Equal("return", result.Arguments);
            Assert.Equal("Pongs the ping", result.Description);
            Assert.IsType<PingSkill>(result.Skill);
        }

        [Fact]
        public async Task ResolvesHiddenBuiltInSkill()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.BuiltinSkillRegistry.AddSkill(new FailSkill());
            await env.CreateAliasAsync("pong", "ping", "return", description: "Pongs the ping");
            var manifest = env.Activate<SkillManifest>();

            var result = await manifest.ResolveSkillAsync(
                "throw-exception",
                organization,
                new FakeFeatureActor());

            Assert.NotNull(result);
            Assert.Equal("throw-exception", result.Name);
            Assert.Equal("", result.Arguments);
            Assert.Equal("Throws an exception. Used for testing.", result.Description);
            Assert.IsType<FailSkill>(result.Skill);
        }

        [Fact]
        public async Task ResolvesUserCallSkillForUserDefinedSkill()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.BuiltinSkillRegistry.AddSkill(env.Activate<RemoteSkillCallSkill>());
            await env.CreateSkillAsync("pug", description: "Returns a random pug");
            var manifest = env.Activate<SkillManifest>();

            var result = await manifest.ResolveSkillAsync(
                "pug",
                organization, new FakeFeatureActor());

            Assert.NotNull(result);
            Assert.Equal("pug", result.Name);
            Assert.Equal("pug", result.Arguments);
            Assert.Equal("Returns a random pug", result.Description);
            Assert.IsType<RemoteSkillCallSkill>(result.Skill);
        }

        [Fact]
        public async Task ResolvesListSkill()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.BuiltinSkillRegistry.AddSkills(
                env.Activate<RemoteSkillCallSkill>(),
                env.Activate<ListSkill>());
            await env.CreateListAsync("deepthought", description: "Deep thoughts by Jack Handey");
            await env.CreateSkillAsync("pug");
            var manifest = env.Activate<SkillManifest>();

            var result = await manifest.ResolveSkillAsync(
                "deepthought",
                organization, new FakeFeatureActor());

            Assert.NotNull(result);
            Assert.Equal("list", result.Name);
            Assert.Equal("deepthought", result.Arguments);
            Assert.Equal("Deep thoughts by Jack Handey", result.Description);
            Assert.IsType<ListSkill>(result.Skill);
        }

        [Fact]
        public async Task ResolvesAliasToUserSkill()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.BuiltinSkillRegistry.AddSkill(env.Activate<RemoteSkillCallSkill>());
            await env.CreateSkillAsync("pug");
            await env.CreateAliasAsync("dog", "pug", "bomb", "returns pugs, lots of pugs");
            var manifest = env.Activate<SkillManifest>();

            var result = await manifest.ResolveSkillAsync(
                "dog",
                organization, new FakeFeatureActor());

            Assert.NotNull(result);
            Assert.Equal("pug", result.Name);
            Assert.Equal("pug bomb", result.Arguments);
            Assert.Equal("returns pugs, lots of pugs", result.Description);
            Assert.IsType<RemoteSkillCallSkill>(result.Skill);
        }

        [Fact]
        public async Task ResolvesFeatureFlaggedBuiltInSkillOnlyWhenFlagEnabled()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            env.BuiltinSkillRegistry.AddSkills(env.Activate<ListSkill>(), new FeatureFlagTestSkill());
            await env.CreateListAsync(FeatureFlagTestSkill.Name, description: "Feature Flag LIST");
            await env.CreateAliasAsync("dog", "pug", "bomb", "returns pugs, lots of pugs");
            var manifest = env.Activate<SkillManifest>();
            var actor = new FakeFeatureActor(user.PlatformUserId, organization);

            // Feature is enabled globally
            var result = await manifest.ResolveSkillAsync(FeatureFlagTestSkill.Name, organization, actor);
            Assert.NotNull(result);
            Assert.IsType<FeatureFlagTestSkill>(result.Skill);

            // Feature is disabled globally
            env.Features.Set(FeatureFlagTestSkill.FlagName, false);
            result = await manifest.ResolveSkillAsync(FeatureFlagTestSkill.Name, organization, actor);
            Assert.NotNull(result);
            Assert.IsType<ListSkill>(result.Skill);

            // Feature is enabled for the user
            env.Features.Set(
                FeatureFlagTestSkill.FlagName,
                user.PlatformUserId,
                true);
            result = await manifest.ResolveSkillAsync(FeatureFlagTestSkill.Name, organization, actor);
            Assert.NotNull(result);
            Assert.IsType<FeatureFlagTestSkill>(result.Skill);

            // Feature is enabled for the group
            env.Features.Clear();
            env.Features.Set(FeatureFlagTestSkill.FlagName, false);
            env.Features.Set(
                FeatureFlagTestSkill.FlagName,
                FeatureHelper.GroupForPlatformId(organization.PlatformId),
                true);
            result = await manifest.ResolveSkillAsync(FeatureFlagTestSkill.Name, organization, actor);
            Assert.NotNull(result);
            Assert.IsType<FeatureFlagTestSkill>(result.Skill);
        }
    }

    [Skill(Name, RequirePlanFeature = PlanFeature.ConversationTracking)]
    class PlanFeatureTestSkill : ISkill
    {
        public const string Name = "plan-feature-test";

        public Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void BuildUsageHelp(UsageBuilder usage)
        {
        }
    }

    [Skill(Name, RequireFeatureFlag = FlagName)]
    class FeatureFlagTestSkill : ISkill
    {
        public const string Name = "feature-flag-test";
        public const string FlagName = "TestFeatureFlag";

        public Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void BuildUsageHelp(UsageBuilder usage)
        {
        }
    }
}
