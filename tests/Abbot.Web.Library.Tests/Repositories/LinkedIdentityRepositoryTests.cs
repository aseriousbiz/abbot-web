using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Repositories;
using Xunit;

namespace Abbot.Web.Library.Tests.Repositories;

public class LinkedIdentityRepositoryTests
{
    public class TheGetLinkedIdentityAsyncMethod
    {
        [Fact]
        public async Task CanLookupByMemberAndType()
        {
            var env = TestEnvironment.Create();
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity()
            {
                Organization = env.TestData.Organization,
                Member = env.TestData.Member,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "123",
                ExternalName = "Test User",
                ExternalMetadata = "{ \"Role\": \"Admin\" }",
            });

            await env.Db.SaveChangesAsync();

            var repo = env.Activate<LinkedIdentityRepository>();
            var (identity, metadata) = await repo.GetLinkedIdentityAsync<ZendeskUserMetadata>(env.TestData.Organization, env.TestData.Member, LinkedIdentityType.Zendesk);

            Assert.NotNull(identity);
            Assert.Equal("123", identity.ExternalId);
            Assert.Equal(LinkedIdentityType.Zendesk, identity.Type);
            Assert.Same(env.TestData.Member, identity.Member);
            Assert.Same(env.TestData.Organization, identity.Organization);
            Assert.Same("Test User", identity.ExternalName);

            Assert.NotNull(metadata);
            Assert.Equal("Admin", metadata.Role);
        }

        [Fact]
        public async Task CanLookupByTypeAndExternalId()
        {
            var env = TestEnvironment.Create();
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity()
            {
                Organization = env.TestData.Organization,
                Member = env.TestData.Member,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "123",
                ExternalMetadata = "{ \"Role\": \"Admin\" }",
            });

            await env.Db.SaveChangesAsync();

            var repo = env.Activate<LinkedIdentityRepository>();
            var (identity, metadata) = await repo.GetLinkedIdentityAsync<ZendeskUserMetadata>(env.TestData.Organization, LinkedIdentityType.Zendesk, "123");

            Assert.NotNull(identity);
            Assert.Equal("123", identity.ExternalId);
            Assert.Equal(LinkedIdentityType.Zendesk, identity.Type);
            Assert.Same(env.TestData.Member, identity.Member);
            Assert.Same(env.TestData.Organization, identity.Organization);

            Assert.NotNull(metadata);
            Assert.Equal("Admin", metadata.Role);
        }

        [Fact]
        public async Task ReturnsNullIfNoMatchesByMemberAndType()
        {
            var env = TestEnvironment.Create();
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity()
            {
                Organization = env.TestData.Organization,
                Member = env.TestData.Member,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "123",
            });

            await env.Db.SaveChangesAsync();

            var repo = env.Activate<LinkedIdentityRepository>();
            var (identity, metadata) = await repo.GetLinkedIdentityAsync<ZendeskUserMetadata>(env.TestData.Organization, env.TestData.ForeignMember, LinkedIdentityType.Zendesk);
            Assert.Null(identity);
            Assert.Null(metadata);
        }

        [Fact]
        public async Task ReturnsNullIfNoMatchesByTypeAndExternalId()
        {
            var env = TestEnvironment.Create();
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity()
            {
                Organization = env.TestData.Organization,
                Member = env.TestData.Member,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "123",
            });

            await env.Db.SaveChangesAsync();

            var repo = env.Activate<LinkedIdentityRepository>();
            var (identity, metadata) = await repo.GetLinkedIdentityAsync<ZendeskUserMetadata>(env.TestData.Organization, LinkedIdentityType.Zendesk, "456");
            Assert.Null(identity);
            Assert.Null(metadata);
        }

        [Fact]
        public async Task IdentitiesInDifferentOrgsDoNotInterfere()
        {
            var env = TestEnvironment.Create();
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity()
            {
                Organization = env.TestData.ForeignOrganization,
                Member = env.TestData.ForeignMember,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "123",
            });

            await env.Db.SaveChangesAsync();

            var repo = env.Activate<LinkedIdentityRepository>();

            var (identity, _) = await repo.GetLinkedIdentityAsync<ZendeskUserMetadata>(env.TestData.Organization, LinkedIdentityType.Zendesk, "123");
            Assert.Null(identity);

            // Even though we're looking for ForeignMember's Zendesk identity, we're doing it from the wrong org.
            (identity, _) = await repo.GetLinkedIdentityAsync<ZendeskUserMetadata>(env.TestData.Organization,
                env.TestData.ForeignMember,
                LinkedIdentityType.Zendesk);
            Assert.Null(identity);

            (identity, _) = await repo.GetLinkedIdentityAsync<ZendeskUserMetadata>(env.TestData.ForeignOrganization,
                env.TestData.ForeignMember,
                LinkedIdentityType.Zendesk);
            Assert.NotNull(identity);
        }
    }

    public class TheLinkIdentityAsyncMethod
    {
        [Fact]
        public async Task FailsIfMemberAlreadyHasLinkedIdentityOfRequestedType()
        {
            var env = TestEnvironment.Create();
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity()
            {
                Organization = env.TestData.Organization,
                Member = env.TestData.Member,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "123",
            });

            await env.Db.SaveChangesAsync();

            using var scope = env.ActivateInNewScope<LinkedIdentityRepository>(out var repo);

            var result = await repo.LinkIdentityAsync(env.TestData.Organization, env.TestData.Member, LinkedIdentityType.Zendesk, "456");
            Assert.False(result.IsSuccess);
            Assert.Equal(EntityResultType.Conflict, result.Type);
            Assert.Equal("User already linked to another Zendesk User.", result.ErrorMessage);
            Assert.Equal("123", result.Entity?.ExternalId);
            Assert.Single(await env.Db.LinkedIdentities.ToListAsync());
        }

        [Fact]
        public async Task FailsIfIdentityAlreadyLinkedToDifferentUser()
        {
            var env = TestEnvironment.Create();
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity()
            {
                Organization = env.TestData.Organization,
                Member = env.TestData.Member,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "123",
            });

            await env.Db.SaveChangesAsync();

            using var scope = env.ActivateInNewScope<LinkedIdentityRepository>(out var repo);

            var result = await repo.LinkIdentityAsync(env.TestData.Organization, env.TestData.ForeignMember, LinkedIdentityType.Zendesk, "123");
            Assert.False(result.IsSuccess);
            Assert.Equal(EntityResultType.Conflict, result.Type);
            Assert.Equal("Another user is already linked to this Zendesk User.", result.ErrorMessage);
            Assert.Equal("123", result.Entity?.ExternalId);
            Assert.Single(await env.Db.LinkedIdentities.ToListAsync());
        }

        [Fact]
        public async Task LinksIdentityToUserIfPreconditionsMet()
        {
            var env = TestEnvironment.Create();
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity
            {
                // Even though there's an existing identity using External ID 456, and for this user,
                // it belongs to a different org, so it's fine for us to create a new one.
                Organization = env.TestData.ForeignOrganization,
                Member = env.TestData.ForeignMember,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "456",
                ExternalName = "External User",
            });

            await env.Db.SaveChangesAsync();

            var repo = env.Activate<LinkedIdentityRepository>();
            var result = await repo.LinkIdentityAsync(
                env.TestData.Organization,
                env.TestData.ForeignMember,
                LinkedIdentityType.Zendesk,
                "456",
                "External User",
                new ZendeskUserMetadata()
                {
                    Role = "Admin",
                });
            Assert.True(result.IsSuccess);
            Assert.Equal(EntityResultType.Success, result.Type);
            Assert.Equal("456", result.Entity.ExternalId);
            Assert.Collection(await env.Db.LinkedIdentities.ToListAsync(),
                id => Assert.Same(env.TestData.ForeignOrganization, id.Organization),
                id => {
                    Assert.Same(env.TestData.Organization, id.Organization);
                    Assert.Same(env.TestData.ForeignMember, id.Member);
                    Assert.Equal(LinkedIdentityType.Zendesk, id.Type);
                    Assert.Equal("456", id.ExternalId);
                    Assert.Equal("External User", id.ExternalName);
                    Assert.Equal("{\"Role\":\"Admin\",\"Subdomain\":null,\"IsFacade\":false,\"KnownOrganizationIds\":[]}", id.ExternalMetadata);
                });
        }
    }

    public class TheRemoveIdentityAsyncMethod
    {
        [Fact]
        public async Task RemovesTheLinkedIdentity()
        {
            var env = TestEnvironment.Create();
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity()
            {
                Organization = env.TestData.Organization,
                Member = env.TestData.Member,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "123",
                ExternalMetadata = "{}",
            });

            await env.Db.SaveChangesAsync();

            var repo = env.Activate<LinkedIdentityRepository>();
            var (identity, metadata) = await repo.GetLinkedIdentityAsync<ZendeskUserMetadata>(env.TestData.Organization, env.TestData.Member, LinkedIdentityType.Zendesk);
            Assert.NotNull(identity);
            Assert.NotNull(metadata);
            await repo.RemoveIdentityAsync(identity);
            (identity, metadata) = await repo.GetLinkedIdentityAsync<ZendeskUserMetadata>(env.TestData.Organization, env.TestData.Member, LinkedIdentityType.Zendesk);
            Assert.Null(identity);
            Assert.Null(metadata);
            Assert.Empty(await env.Db.LinkedIdentities.ToListAsync());
        }
    }

    public class TheGetAllLinkedIdentitiesForMemberAsyncMethod
    {
        [Fact]
        public async Task RetrievesLinkedIdentitiesForUserAcrossAllOrgs()
        {
            var env = TestEnvironment.Create();
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity()
            {
                Organization = env.TestData.Organization,
                Member = env.TestData.Member,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "123",
            });
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity()
            {
                Organization = env.TestData.ForeignOrganization,
                Member = env.TestData.Member,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "456",
            });
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity()
            {
                Organization = env.TestData.ForeignOrganization,
                Member = env.TestData.ForeignMember,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "789",
            });
            await env.Db.SaveChangesAsync();

            var repo = env.Activate<LinkedIdentityRepository>();

            Assert.Collection(await repo.GetAllLinkedIdentitiesForMemberAsync(env.TestData.Member),
                id => {
                    Assert.Same(env.TestData.ForeignOrganization, id.Organization);
                    Assert.Same(env.TestData.Member, id.Member);
                    Assert.Equal(LinkedIdentityType.Zendesk, id.Type);
                    Assert.Equal("456", id.ExternalId);
                },
                id => {
                    Assert.Same(env.TestData.Organization, id.Organization);
                    Assert.Same(env.TestData.Member, id.Member);
                    Assert.Equal(LinkedIdentityType.Zendesk, id.Type);
                    Assert.Equal("123", id.ExternalId);
                });
        }
    }

    public class TheClearIdentitiesAsyncMethod
    {
        [Fact]
        public async Task RemovesLinkedIdentitiesForTheOrgAndIntegrationType()
        {
            var env = TestEnvironment.Create();
            var extraMember = await env.CreateMemberInAgentRoleAsync();
            var extraMember2 = await env.CreateMemberInAgentRoleAsync();
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity
            {
                Organization = env.TestData.Organization,
                Member = env.TestData.Member,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "123",
            });
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity
            {
                Organization = env.TestData.Organization,
                Member = extraMember,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "124",
            });
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity
            {
                Organization = env.TestData.ForeignOrganization,
                Member = env.TestData.ForeignMember,
                Type = LinkedIdentityType.Zendesk,
                ExternalId = "125",
            });
            await env.Db.LinkedIdentities.AddAsync(new LinkedIdentity
            {
                Organization = env.TestData.Organization,
                Member = extraMember2,
                Type = LinkedIdentityType.Zendesk - 1,
                ExternalId = "126",
            });
            await env.Db.SaveChangesAsync();
            var repo = env.Activate<LinkedIdentityRepository>();
            Assert.Equal(4, await env.Db.LinkedIdentities.CountAsync());

            await repo.ClearIdentitiesAsync(env.TestData.Organization, LinkedIdentityType.Zendesk);

            Assert.Collection(await env.Db.LinkedIdentities.OrderBy(i => i.ExternalId).ToListAsync(),
                i1 => Assert.Equal("125", i1.ExternalId),
                i2 => Assert.Equal("126", i2.ExternalId));
        }
    }
}
