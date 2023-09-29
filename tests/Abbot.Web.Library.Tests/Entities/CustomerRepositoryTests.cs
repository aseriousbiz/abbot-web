using Abbot.Common.TestHelpers;

public class CustomerRepositoryTests
{
    public class TheGetCustomerSegmentsByNamesAsyncMethod
    {
        [Fact]
        public async Task GetsAllSegmentsByNames()
        {
            var env = TestEnvironment.Create();
            var repository = env.Activate<CustomerRepository>();
            var tag0 = await repository.CreateCustomerSegmentAsync("TaG0", env.TestData.Member, env.TestData.Organization);
            var tag1 = await repository.CreateCustomerSegmentAsync("tAG1", env.TestData.Member, env.TestData.Organization);

            var tags = await repository.GetCustomerSegmentsByNamesAsync(
                new[] { "TAG0", "tag1", "tag2" },
                env.TestData.Organization);

            Assert.True(tags[0].IsSuccess);
            Assert.True(tags[1].IsSuccess);
            Assert.False(tags[2].IsSuccess);
            Assert.Equal(tag0.Id, tags[0].Entity!.Id);
            Assert.Equal(tag1.Id, tags[1].Entity!.Id);
            Assert.Null(tags[2].Entity);
        }
    }
}
