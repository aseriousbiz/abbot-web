using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;

namespace Serious.Abbot.Playbooks;

public enum Weekday
{
    Sunday = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6,
}

[JsonConverter(typeof(ScheduleJsonConverter))]
public abstract record Schedule
{
    public abstract string ToCronString();
}

public record HourlySchedule(int Minute) : Schedule
{
    public override string ToCronString() => $"{Minute} * * * *";
}

public record DailySchedule(int Hour, int Minute) : Schedule
{
    public override string ToCronString() => $"{Minute} {Hour} * * *";
}

public record WeeklySchedule(int Hour, int Minute, IReadOnlyList<Weekday> Weekdays) : Schedule
{
    public override string ToCronString() => $"{Minute} {Hour} * * {string.Join(",", Weekdays.Select(w => (int)w))}";
}

public record MonthlySchedule(int Hour, int Minute, DayOfMonth DayOfMonth) : Schedule
{
    public override string ToCronString() => $"{Minute} {Hour} {DayOfMonth.Value} * *";
}

public record AdvancedSchedule(string Cron) : Schedule
{
    public override string ToCronString() => Cron;
}

// TODO: At some point, we'll want to support special days like "last day of the month" and "last weekday of the month".
[JsonConverter(typeof(DayOfMonthConverter))]
public record DayOfMonth(int Value);

public class DayOfMonthConverter : JsonConverter<DayOfMonth>
{
    public override void WriteJson(JsonWriter writer, DayOfMonth? value, JsonSerializer serializer)
    {
        writer.WriteValue(value?.Value);
    }

    public override DayOfMonth? ReadJson(JsonReader reader, Type objectType, DayOfMonth? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType is not JsonToken.Integer || reader.Value is not long l)
        {
            throw new InvalidOperationException(
                $"Could not read DayOfMonth from {nameof(JsonToken)}.{reader.TokenType}.");
        }

        return new((int)l);
    }
}

public class ScheduleJsonConverter : JsonConverter<Schedule>
{
    public override void WriteJson(JsonWriter writer, Schedule? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var serialized = new SerializedSchedule(value);
        serializer.Serialize(writer, serialized);
    }

    public override Schedule? ReadJson(JsonReader reader, Type objectType, Schedule? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        var serialized = serializer.Deserialize<SerializedSchedule>(reader);

        if (serialized is null)
        {
            return null;
        }

        switch (serialized.Type?.ToLowerInvariant())
        {
            case "hourly":
                return new HourlySchedule(
                    serialized.Minute
                    ?? throw new JsonException($"Missing required property '{nameof(serialized.Minute)}'"));
            case "daily":
                return new DailySchedule(
                    serialized.Hour
                    ?? throw new JsonException($"Missing required property '{nameof(serialized.Hour)}'"),
                    serialized.Minute
                    ?? throw new JsonException($"Missing required property '{nameof(serialized.Minute)}'"));
            case "weekly":
                return new WeeklySchedule(
                    serialized.Hour
                    ?? throw new JsonException($"Missing required property '{nameof(serialized.Hour)}'"),
                    serialized.Minute
                    ?? throw new JsonException($"Missing required property '{nameof(serialized.Minute)}'"),
                    serialized.Weekdays
                    ?? throw new JsonException($"Missing required property '{nameof(serialized.Weekdays)}'"));
            case "monthly":
                return new MonthlySchedule(
                    serialized.Hour
                    ?? throw new JsonException($"Missing required property '{nameof(serialized.Hour)}'"),
                    serialized.Minute
                    ?? throw new JsonException($"Missing required property '{nameof(serialized.Minute)}'"),
                    serialized.DayOfMonth
                    ?? throw new JsonException($"Missing required property '{nameof(serialized.DayOfMonth)}'"));
            case "advanced":
                return new AdvancedSchedule(serialized.Cron
                                            ?? throw new JsonException(
                                                $"Missing required property '{nameof(serialized.Cron)}'"));
            default:
                throw new JsonException($"Unknown schedule type '{serialized.Type}'");
        }
    }

    class SerializedSchedule
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("hour")]
        public int? Hour { get; set; }

        [JsonProperty("minute")]
        public int? Minute { get; set; }

        [JsonProperty("weekdays")]
        public IReadOnlyList<Weekday>? Weekdays { get; set; }

        [JsonProperty("dayOfMonth")]
        public DayOfMonth? DayOfMonth { get; set; }

        [JsonProperty("cron")]
        public string? Cron { get; set; }

        [JsonConstructor]
        public SerializedSchedule()
        {
        }

        public SerializedSchedule(Schedule schedule)
        {
            switch (schedule)
            {
                case HourlySchedule h:
                    Type = "hourly";
                    Minute = h.Minute;
                    break;
                case DailySchedule d:
                    Type = "daily";
                    Hour = d.Hour;
                    Minute = d.Minute;
                    break;
                case WeeklySchedule w:
                    Type = "weekly";
                    Hour = w.Hour;
                    Minute = w.Minute;
                    Weekdays = w.Weekdays;
                    break;
                case MonthlySchedule m:
                    Type = "monthly";
                    Hour = m.Hour;
                    Minute = m.Minute;
                    DayOfMonth = m.DayOfMonth;
                    break;
                case AdvancedSchedule a:
                    Type = "advanced";
                    Cron = a.Cron;
                    break;
                default:
                    throw new UnreachableException($"Unknown schedule type: {schedule.GetType()}");
            }
        }
    }
}
