using Abbot.Common.TestHelpers;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Integrations.Zendesk.Models;

namespace Abbot.Web.Library.Tests.Integrations.Zendesk;

public class ZendeskResolverTests
{
    public class TheResolveZendeskIdentityAsyncMethod
    {
        static readonly string TestSubdomain = "subdomain";
        static readonly long TestUserId = 1337;
        static readonly string TestUserUrl = $"https://{TestSubdomain}.zendesk.com/api/v2/users/{TestUserId}.json";
        static readonly string BogusUserUrl = $"https://{TestSubdomain}.zendesk.com/api/v2/users/9999.json";

        static void AssertCreateUserRequest(UserMessage? creationRequest, Member member, long? organizationId,
            string botName)
        {
            var expectedEmail =
                $"{member.User.PlatformUserId}@{member.Organization.Slug}.{member.Organization.PlatformId}.{member.Organization.PlatformType.ToString().ToLowerInvariant()}.{WebConstants.EmailDomain}";

            Assert.NotNull(creationRequest);
            Assert.Equal($"{member.DisplayName} (via {botName})", creationRequest.Body?.Name);
            Assert.Equal(organizationId, creationRequest.Body?.OrganizationId);
            Assert.Equal("end-user", creationRequest.Body?.Role);
            Assert.Equal($"Serious.Abbot:{member.User.PlatformUserId}", creationRequest.Body?.ExternalId);
            Assert.Equal(expectedEmail, creationRequest.Body?.Email);
            Assert.True(creationRequest.Body?.Verified);
        }

        [Fact]
        public async Task ReturnsLinkedIdentityFromDatabaseIfOneIsPresent()
        {
            var env = TestEnvironment.Create();
            var identities = env.Get<ILinkedIdentityRepository>();
            await identities.LinkIdentityAsync(env.TestData.Organization,
                env.TestData.ForeignMember,
                LinkedIdentityType.Zendesk,
                TestUserUrl,
                "Test User",
                // Only needs to exist; we don't check Role/Email/Subdomain
                new ZendeskUserMetadata());

            // Does not need to update
            using var _ = env.Db.RaiseIfSaved();

            var client = Substitute.For<IZendeskClient>();
            var expected = new ZendeskUser()
            {
                Role = "yoop"
            };

            client.GetUserAsync(TestUserId)
                .Returns(Task.FromResult(new UserMessage
                {
                    Body = expected
                }));

            var resolver = env.Activate<ZendeskResolver>();
            var actual = await resolver.ResolveZendeskIdentityAsync(
                client,
                env.TestData.Organization,
                env.TestData.ForeignMember,
                null);

            Assert.Same(expected, actual);
        }

        [Fact]
        public async Task RefreshesLinkedIdentityFromApiIfNotFullyPopulated()
        {
            var env = TestEnvironment.Create();
            var identities = env.Get<ILinkedIdentityRepository>();
            await identities.LinkIdentityAsync(env.TestData.Organization,
                env.TestData.ForeignMember,
                LinkedIdentityType.Zendesk,
                TestUserUrl);

            var client = Substitute.For<IZendeskClient>();
            var expected = new ZendeskUser
            {
                Email = "from@api.zendesk.com",
                Name = "API name",
                Role = "API role",
            };

            client.GetUserAsync(1337).Returns(Task.FromResult(new UserMessage
            {
                Body = expected,
            }));

            var resolver = env.Activate<ZendeskResolver>();
            var actual = await resolver.ResolveZendeskIdentityAsync(
                client,
                env.TestData.Organization,
                env.TestData.ForeignMember,
                null);

            Assert.Same(expected, actual);

            using var _ = env.GetRequiredServiceInNewScope<ILinkedIdentityRepository>(out var isolated);
            var (linkedIdentity, metadata) = await isolated.GetLinkedIdentityAsync<ZendeskUserMetadata>(
                env.TestData.Organization,
                env.TestData.ForeignMember,
                LinkedIdentityType.Zendesk);

            Assert.NotNull(linkedIdentity);
            Assert.Equal("API name", linkedIdentity.ExternalName);
            Assert.NotNull(metadata);
            Assert.Equal("API role", metadata.Role);
            Assert.Equal(TestSubdomain, metadata.Subdomain);
            Assert.False(metadata.IsFacade);
        }

        [Fact]
        public async Task LooksUpExistingUserUsingSavedEmail()
        {
            var env = TestEnvironment.Create();
            env.TestData.User.Email = "test@example.com";
            await env.Db.SaveChangesAsync();

            var identities = env.Get<ILinkedIdentityRepository>();
            var client = Substitute.For<IZendeskClient>();
            var expected = new ZendeskUser()
            {
                Name = "Test User",
                Role = "end-user",
                Email = "Test@Example.com",
                Url = TestUserUrl,
            };

            client.SearchUsersAsync($"email:{env.TestData.Member.User.Email}", null)
                .Returns(new UserListMessage()
                {
                    Body = new[]
                    {
                        // Make sure that even if Zendesk's query goes a bit wonky, we find the _exact matching email_.
                        new ZendeskUser()
                        {
                            Name = "Bogus User",
                            Role = "end-user",
                            Email = "bogus@example.com",
                            Url = BogusUserUrl,
                        },
                        expected,
                        // We take the first exact match on email, so this one shouldn't be used.
                        new ZendeskUser()
                        {
                            Name = "Bogus User 2",
                            Role = "end-user",
                            Email = env.TestData.Member.User.Email.Require(),
                            Url = BogusUserUrl,
                        },
                    }
                });

            var resolver = env.Activate<ZendeskResolver>();
            var actual = await resolver.ResolveZendeskIdentityAsync(
                client,
                env.TestData.Organization,
                env.TestData.Member,
                null);

            Assert.Equal(expected, actual);
            ;

            var (linkedIdentity, metadata) = await identities.GetLinkedIdentityAsync<ZendeskUserMetadata>(
                env.TestData.Organization,
                env.TestData.Member,
                LinkedIdentityType.Zendesk);

            Assert.NotNull(linkedIdentity);
            Assert.Equal(TestUserUrl, linkedIdentity.ExternalId);
            Assert.False(metadata?.IsFacade);
        }

        [Fact]
        public async Task LooksUpExistingUserWithEmailFromSlackIfNotSaved()
        {
            var env = TestEnvironment.Create();
            Assert.Null(env.TestData.ForeignUser.Email);

            var foreignEmail = "test@example.com";
            env.SlackApi.AddUserInfoResponse(
                env.TestData.Organization.ApiToken!.Reveal(),
                new()
                {
                    Id = env.TestData.ForeignUser.PlatformUserId,
                    Profile = new()
                    {
                        Email = foreignEmail,
                    },
                });
            ;

            var identities = env.Get<ILinkedIdentityRepository>();
            var client = Substitute.For<IZendeskClient>();
            var expected = new ZendeskUser()
            {
                Name = "Test User",
                Role = "end-user",
                Email = "Test@Example.com",
                Url = TestUserUrl,
            };

            client.SearchUsersAsync($"email:{foreignEmail}", null)
                .Returns(new UserListMessage()
                {
                    Body = new[]
                    {
                        // Make sure that even if Zendesk's query goes a bit wonky, we find the _exact matching email_.
                        new ZendeskUser()
                        {
                            Name = "Bogus User",
                            Role = "end-user",
                            Email = "bogus@example.com",
                            Url = BogusUserUrl,
                        },
                        expected,
                        // We take the first exact match on email, so this one shouldn't be used.
                        new ZendeskUser()
                        {
                            Name = "Bogus User 2",
                            Role = "end-user",
                            Email = foreignEmail,
                            Url = BogusUserUrl,
                        },
                    }
                });

            var resolver = env.Activate<ZendeskResolver>();
            var actual = await resolver.ResolveZendeskIdentityAsync(
                client,
                env.TestData.Organization,
                env.TestData.ForeignMember,
                null);

            Assert.Equal(expected, actual);

            var (linkedIdentity, metadata) = await identities.GetLinkedIdentityAsync<ZendeskUserMetadata>(
                env.TestData.Organization,
                env.TestData.ForeignMember,
                LinkedIdentityType.Zendesk);

            Assert.NotNull(linkedIdentity);
            Assert.Equal(TestUserUrl, linkedIdentity.ExternalId);
            Assert.False(metadata?.IsFacade);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(1234L, null)]
        [InlineData(null, "TestBot")]
        [InlineData(1234L, "TestBot")]
        public async Task CreatesAndLinksAbbotFacadeForForeignUserIfNoFacadeExists(long? organizationId,
            string? botName)
        {
            var env = TestEnvironment.Create();
            if (botName is not null)
            {
                env.TestData.Organization.BotName = botName;
                await env.Db.SaveChangesAsync();
            }

            var identities = env.Get<ILinkedIdentityRepository>();
            var client = Substitute.For<IZendeskClient>();
            UserMessage? creationRequest = null;
            var expected = new ZendeskUser()
            {
                Name = "Test User",
                Email = "test@example.com",
                Role = "end-user",
                Url = TestUserUrl
            };

            client.CreateOrUpdateUserAsync(Arg.Do<UserMessage>(u => creationRequest = u))
                .Returns(new UserMessage
                {
                    Body = expected
                });

            var resolver = env.Activate<ZendeskResolver>();
            var actual = await resolver.ResolveZendeskIdentityAsync(
                client,
                env.TestData.Organization,
                env.TestData.ForeignMember,
                organizationId);

            Assert.Same(expected, actual);

            AssertCreateUserRequest(creationRequest,
                env.TestData.ForeignMember,
                organizationId,
                env.TestData.Organization.BotName.Require());

            var (linkedIdentity, metadata) = await identities.GetLinkedIdentityAsync<ZendeskUserMetadata>(
                env.TestData.Organization,
                env.TestData.ForeignMember,
                LinkedIdentityType.Zendesk);

            Assert.NotNull(linkedIdentity);
            Assert.Equal(TestUserUrl, linkedIdentity.ExternalId);

            Assert.NotNull(metadata);
            Assert.Equal("end-user", metadata.Role);
            Assert.Equal(TestSubdomain, metadata.Subdomain);
            Assert.True(metadata.IsFacade);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("nonmatch@example.com", null)]
        [InlineData(null, 42L)]
        [InlineData("nonmatch@example.com", 42L)]
        public async Task CreatesFacadeUserForHomeUserWithNoMatchingZendeskUser(string? email, long? organizationId)
        {
            var env = TestEnvironment.Create();
            env.TestData.User.Email = email;
            await env.Db.SaveChangesAsync();

            var identities = env.Get<ILinkedIdentityRepository>();
            var client = Substitute.For<IZendeskClient>();
            client.SearchUsersAsync($"email:{email}", null)
                .Returns(new UserListMessage
                {
                    Body = new[]
                    {
                        // Again, even if we "return" results, we want to make sure we're looking for a matching user.
                        new ZendeskUser()
                        {
                            Email = "bogus@example.com",
                            Url = BogusUserUrl,
                        },
                    }
                });

            UserMessage? creationRequest = null;
            var expected = new ZendeskUser()
            {
                Name = "Test User",
                Email = "test@example.com",
                Role = "end-user",
                Url = TestUserUrl
            };

            client.CreateOrUpdateUserAsync(Arg.Do<UserMessage>(u => creationRequest = u))
                .Returns(new UserMessage()
                {
                    Body = expected
                });

            var resolver = env.Activate<ZendeskResolver>();
            var actual = await resolver.ResolveZendeskIdentityAsync(
                client,
                env.TestData.Organization,
                env.TestData.Member,
                organizationId);

            Assert.Same(expected, actual);

            AssertCreateUserRequest(creationRequest,
                env.TestData.Member,
                organizationId,
                env.TestData.Organization.BotName.Require());

            var (linkedIdentity, metadata) = await identities.GetLinkedIdentityAsync<ZendeskUserMetadata>(
                env.TestData.Organization,
                env.TestData.Member,
                LinkedIdentityType.Zendesk);

            Assert.NotNull(linkedIdentity);
            Assert.Equal(TestUserUrl, linkedIdentity.ExternalId);

            Assert.NotNull(metadata);
            Assert.Equal("end-user", metadata.Role);
            Assert.Equal(TestSubdomain, metadata.Subdomain);
            Assert.True(metadata.IsFacade);
        }
    }
}
