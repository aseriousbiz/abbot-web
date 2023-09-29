using Abbot.Common.TestHelpers;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.AppStartup;

public class DataTransitionSeederTests
{
    public class TheRunForeignUserCleanupAsyncMethod
    {
        [Fact]
        public async Task ProperlyClearsForeignOrgMemberAndUserData()
        {
            var env = TestEnvironment.Create();

            var localMember = await env.CreateMemberAsync(
                org: env.TestData.Organization,
                email: "we@do.want.to.store.this");
            var foreignMember = await env.CreateMemberAsync(
                org: env.TestData.ForeignOrganization,
                email: "we@dont.want.to.store.this");

            // Create a user that has both a foreign member and a home member.
            // We should keep their data because they have a home member.
            var homeMemberForMultiUser = await env.CreateMemberAsync(
                org: env.TestData.Organization,
                email: "we@should.store.this");

            var (foreignMemberForMultiUser, entityState) =
                homeMemberForMultiUser.User.GetOrCreateMemberInstanceForOrganization(env.TestData.ForeignOrganization);
            Assert.Equal(EntityEnsureState.Creating, entityState);
            await env.Db.SaveChangesAsync();

            Assert.Equal(2, homeMemberForMultiUser.User.Members.Count);
            Assert.Equal(foreignMemberForMultiUser.UserId, homeMemberForMultiUser.UserId);

            // Now run the job
            var job = env.Activate<RunForeignUserCleanupSeeder>();
            await job.SeedDataAsync();

            // Check the results
            await env.ReloadAsync(localMember, foreignMember);
            Assert.Equal("we@do.want.to.store.this", localMember.User.Email);
            Assert.Null(foreignMember.User.Email);
            Assert.Equal("we@should.store.this", homeMemberForMultiUser.User.Email);
            Assert.Equal("we@should.store.this", foreignMemberForMultiUser.User.Email);
        }
    }
}
