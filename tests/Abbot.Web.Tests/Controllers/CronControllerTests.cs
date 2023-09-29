using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Controllers.InternalApi;
using Xunit;

public class CronControllerTests : ControllerTestBase<CronController>
{
    protected override string? ExpectedArea => InternalApiControllerBase.Area;

    public class TheGetMethod : CronControllerTests
    {
        [Theory]
        [InlineData("*/10 * * * *", "Every 10 minutes, every hour, every day")]
        [InlineData("10 2-10 * * *", "At 10 minutes past the hour, between 02:00 AM and 10:59 AM, every day")]
        public async Task ReturnsCronDescriptionForValidString(string cron, string expected)
        {
            var (_, result) = await InvokeControllerAsync(c => c.Get(cron));

            var jsonResult = Assert.IsType<JsonResult>(result);
            var cronResult = Assert.IsType<CronResult>(jsonResult.Value);
            Assert.True(cronResult.Success);
            Assert.Equal(expected, cronResult.Description);
        }

        [Fact]
        public async Task ReturnsErrorForSixPartCron()
        {
            var (_, result) = await InvokeControllerAsync(c => c.Get("* */10 * * * *"));

            var jsonResult = Assert.IsType<JsonResult>(result);
            var cronResult = Assert.IsType<CronResult>(jsonResult.Value);
            Assert.False(cronResult.Success);
            Assert.Equal("Only five part cron expressions are supported", cronResult.Description);
        }

        [Theory]
        [InlineData(@"*/9 * * * *")]
        [InlineData(@"3/9 * * * *")]
        [InlineData(@"* * * * *")]
        [InlineData(@"1-2 * * * *")]
        [InlineData(@"1-2/9 * * * *")]
        public async Task ReturnsErrorForScheduleMoreOftenThanEveryTenMinutes(string cron)
        {
            var (_, result) = await InvokeControllerAsync(c => c.Get(cron));

            var jsonResult = Assert.IsType<JsonResult>(result);
            var cronResult = Assert.IsType<CronResult>(jsonResult.Value);
            Assert.False(cronResult.Success);
            Assert.Equal("Skills that run more than every ten minutes are not allowed", cronResult.Description);
        }

        [Theory]
        [InlineData(@"0 0 31 2 *")]
        [InlineData(@"0 0 30 2 *")]
        [InlineData(@"0 0 31 4 *")]
        [InlineData(@"0 0 31 6 *")]
        [InlineData(@"0 0 31 9 *")]
        [InlineData(@"0 0 31 11 *")]
        public async Task ReturnsNeverForNeverSchedules(string cron)
        {
            var (_, result) = await InvokeControllerAsync(c => c.Get(cron));

            var jsonResult = Assert.IsType<JsonResult>(result);
            var cronResult = Assert.IsType<CronResult>(jsonResult.Value);
            Assert.True(cronResult.Success);
            Assert.Equal("Never", cronResult.Description);
        }
    }
}
