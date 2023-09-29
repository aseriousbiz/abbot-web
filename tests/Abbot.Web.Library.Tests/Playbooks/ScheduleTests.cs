using Serious.Abbot.Playbooks;
using Serious.Abbot.Serialization;

namespace Abbot.Web.Library.Tests.Playbooks;

public class ScheduleTests
{
    public class TheToCronStringMethod
    {
        [Fact]
        public void ForHourlySchedule()
        {
            // https://crontab.guru/#48_*_*_*_*
            Assert.Equal("48 * * * *", new HourlySchedule(48).ToCronString());
        }

        [Fact]
        public void ForDailySchedule()
        {
            // https://crontab.guru/#48_22_*_*_*
            Assert.Equal("48 22 * * *", new DailySchedule(22, 48).ToCronString());
        }

        [Fact]
        public void ForWeeklySchedule()
        {
            // https://crontab.guru/#48_22_*_*_0,5,2
            Assert.Equal("48 22 * * 0,5,2", new WeeklySchedule(22, 48, new[] { Weekday.Sunday, Weekday.Friday, Weekday.Tuesday }).ToCronString());
        }

        [Fact]
        public void ForMonthlySchedule()
        {
            // https://crontab.guru/#48_22_*_*_0,5,2
            Assert.Equal("48 22 28 * *", new MonthlySchedule(22, 48, new DayOfMonth(28)).ToCronString());
        }

        [Fact]
        public void ForAdvancedSchedule()
        {
            Assert.Equal("Doesn't even need to be valid", new AdvancedSchedule("Doesn't even need to be valid").ToCronString());
        }
    }

    public class DeserializationTests
    {
        [Fact]
        public void CanDeserializeHourlySchedule()
        {
            const string json = """
                {"type": "hourly", "minute": 42}
            """;
            var schedule = AbbotJsonFormat.Default.Deserialize<Schedule>(json);
            var parsed = Assert.IsType<HourlySchedule>(schedule);
            Assert.Equal(42, parsed.Minute);
        }

        [Fact]
        public void CanDeserializeDailySchedule()
        {
            const string json = """
                {"type": "daily", "minute": 42, "hour": 3}
            """;
            var schedule = AbbotJsonFormat.Default.Deserialize<Schedule>(json);
            var parsed = Assert.IsType<DailySchedule>(schedule);
            Assert.Equal(42, parsed.Minute);
            Assert.Equal(3, parsed.Hour);
        }

        [Fact]
        public void CanDeserializeWeeklySchedule()
        {
            const string json = """
                {"type": "weekly", "minute": 42, "hour": 3, "weekdays": ["Sunday", "mOnDAY", "friday"]}
            """;
            var schedule = AbbotJsonFormat.Default.Deserialize<Schedule>(json);
            var parsed = Assert.IsType<WeeklySchedule>(schedule);
            Assert.Equal(42, parsed.Minute);
            Assert.Equal(3, parsed.Hour);
            Assert.Equal(new[] { Weekday.Sunday, Weekday.Monday, Weekday.Friday }, parsed.Weekdays);
        }

        [Fact]
        public void CanDeserializeMonthlySchedule()
        {
            const string json = """
                {"type": "monthly", "minute": 42, "hour": 3, "dayOfMonth": 4}
            """;
            var schedule = AbbotJsonFormat.Default.Deserialize<Schedule>(json);
            var parsed = Assert.IsType<MonthlySchedule>(schedule);
            Assert.Equal(42, parsed.Minute);
            Assert.Equal(3, parsed.Hour);
            Assert.Equal(4, parsed.DayOfMonth.Value);
        }

        [Fact]
        public void CanDeserializeAdvancedSchedule()
        {
            const string json = """
                {"type": "advanced", "cron": "still doesn't need to be valid"}
            """;
            var schedule = AbbotJsonFormat.Default.Deserialize<Schedule>(json);
            var parsed = Assert.IsType<AdvancedSchedule>(schedule);
            Assert.Equal("still doesn't need to be valid", parsed.Cron);
        }
    }
}
