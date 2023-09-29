using System.Globalization;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Services;

public class CoverageHoursResponseTimeCalculatorTests
{
    [Theory]
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-17T01:00:00.0000000Z", "08:00")] // 3/16 9am - 3/16 6pm PDT
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-17T17:00:00.0000000Z", "09:00")] // 3/16 9am - 3/17 10am PDT
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-18T17:00:00.0000000Z", "17:00")] // 3/16 9am - 3/18 10am PDT
    public async Task UsesDefaultWorkingHoursInDefaultTimeZoneWhenNoRespondersConfigured(
        string createdDate,
        string lastStateChangedDate,
        string expectedResponseTimeWorkingHours)
    {
        var env = TestEnvironment.Create();
        var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
        var created = DateTime.Parse(createdDate, null, DateTimeStyles.RoundtripKind);
        var stateChangedOn = DateTime.Parse(lastStateChangedDate, null, DateTimeStyles.RoundtripKind);
        var conversation = await env.CreateConversationAsync(room, "Mock Conversation", created);
        var calculator = env.Activate<CoverageHoursResponseTimeCalculator>();

        var result = await calculator.CalculateResponseTimeAsync(conversation, stateChangedOn);

        var expectedWorkingHoursResponseTime = TimeSpan.Parse(expectedResponseTimeWorkingHours);
        Assert.Equal(expectedWorkingHoursResponseTime, result);
    }

    [Theory]
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-17T01:00:00.0000000Z", "America/Chicago", "08:00")]    // 3/16 11am - 8pm CDT
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-17T17:00:00.0000000Z", "America/Chicago", "15:00")]    // 3/16 11am - 12pm EDT (next day)
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-17T01:00:00.0000000Z", "America/New_York", "07:00")]   // 3/16 12pm - 9pm EDT
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-17T17:00:00.0000000Z", "America/New_York", "15:00")]   // 3/16 12pm - 1pm EDT (next day)
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-18T17:00:00.0000000Z", "America/New_York", "1.05:00")] // 3/16 12pm - 1pm EDT (2 days later)
    public async Task UsesAssignedFirstRespondersWorkingHoursAndTimeZones(
        string createdDate,
        string lastStateChangedDate,
        string responderTimeZoneId,
        string expectedResponseTimeWorkingHours)
    {
        var env = TestEnvironment.Create();
        var firstResponder = await env.CreateMemberInAgentRoleAsync(
            timeZoneId: responderTimeZoneId,
            workingHours: new WorkingHours(new(5, 0), new(19, 0)));
        // Default responder is ignored.
        var defaultResponder = await env.CreateMemberInAgentRoleAsync(isDefaultResponder: true);
        defaultResponder.TimeZoneId = "Pacific/Guam";
        defaultResponder.WorkingHours = new WorkingHours(new(0), new(0)); // Beast works 24hrs!
        var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
        await env.Rooms.AssignMemberAsync(
            room,
            firstResponder,
            RoomRole.FirstResponder,
            env.TestData.Abbot);
        Assert.NotEmpty(room.Assignments);
        var created = DateTime.Parse(createdDate, null, DateTimeStyles.RoundtripKind);
        var stateChangedOn = DateTime.Parse(lastStateChangedDate, null, DateTimeStyles.RoundtripKind);
        var conversation = await env.CreateConversationAsync(room, "Mock Conversation", created);
        var calculator = env.Activate<CoverageHoursResponseTimeCalculator>();

        var result = await calculator.CalculateResponseTimeAsync(conversation, stateChangedOn);

        var expectedWorkingHoursResponseTime = TimeSpan.Parse(expectedResponseTimeWorkingHours);
        Assert.Equal(expectedWorkingHoursResponseTime, result);
    }

    [Theory]
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-17T01:00:00.0000000Z", "America/Chicago", "08:00")]    // 3/16 11am - 8pm CDT
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-17T17:00:00.0000000Z", "America/Chicago", "15:00")]    // 3/16 11am - 12pm EDT (next day)
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-17T01:00:00.0000000Z", "America/New_York", "07:00")]   // 3/16 12pm - 9pm EDT
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-17T17:00:00.0000000Z", "America/New_York", "15:00")]   // 3/16 12pm - 1pm EDT (next day)
    [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-18T17:00:00.0000000Z", "America/New_York", "1.05:00")] // 3/16 12pm - 1pm EDT (2 days later)
    public async Task UsesDefaultFirstRespondersWorkingHoursAndTimeZonesWhenNoFirstRespondersAssigned(
        string createdDate,
        string lastStateChangedDate,
        string responderTimeZoneId,
        string expectedResponseTimeWorkingHours)
    {
        var env = TestEnvironment.Create();
        await env.CreateMemberInAgentRoleAsync(
            timeZoneId: responderTimeZoneId,
            isDefaultResponder: true,
            workingHours: new WorkingHours(new(5, 0), new(19, 0)));
        var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
        Assert.Empty(room.Assignments);
        var created = DateTime.Parse(createdDate, null, DateTimeStyles.RoundtripKind);
        var stateChangedOn = DateTime.Parse(lastStateChangedDate, null, DateTimeStyles.RoundtripKind);
        var conversation = await env.CreateConversationAsync(room, "Mock Conversation", created);
        var calculator = env.Activate<CoverageHoursResponseTimeCalculator>();

        var result = await calculator.CalculateResponseTimeAsync(conversation, stateChangedOn);

        var expectedWorkingHoursResponseTime = TimeSpan.Parse(expectedResponseTimeWorkingHours);
        Assert.Equal(expectedWorkingHoursResponseTime, result);
    }
}
