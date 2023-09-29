using Serious.Payloads;
using Serious.Slack.Converters;
using Xunit;

public class RecordBinderTests
{
    public class TheBindMethod
    {
        public record UnknownPleasures(
            [property: Bind("Track1", "Track1")]
            string TheFirstTrack,

            [property: Bind("Track2", "Track2")]
            string TheSecondTrack);

        [Fact]
        public void CanBindRecord()
        {
            object? ValueGetter(BindAttribute? bindAttribute)
            {
                return bindAttribute?.BlockId switch
                {
                    "Track1" => "Disorder",
                    "Track2" => "Day of the Lords",
                    _ => null
                };
            }
            var recordBinder = new RecordBinder();

            var success = recordBinder.TryBindRecord<UnknownPleasures>(ValueGetter, out var bound);

            Assert.True(success);
            Assert.NotNull(bound);
            Assert.Equal("Disorder", bound.TheFirstTrack);
            Assert.Equal("Day of the Lords", bound.TheSecondTrack);
        }

        public record Nevermind(string Track1, string Track2);

        [Fact]
        public void CanBindRecordByConvention()
        {
            object? ValueGetter(BindAttribute? bindAttribute)
            {
                return bindAttribute?.BlockId switch
                {
                    "Track1" => "Smells Like Teen Spirit",
                    "Track2" => "In Bloom",
                    _ => null
                };
            }
            var recordBinder = new RecordBinder();

            var success = recordBinder.TryBindRecord<Nevermind>(ValueGetter, out var bound);

            Assert.True(success);
            Assert.NotNull(bound);
            Assert.Equal("Smells Like Teen Spirit", bound.Track1);
            Assert.Equal("In Bloom", bound.Track2);
        }


        public record ItTakesANationOfMillionsToHoldUsBack(
            [property: Bind("Track1")]
            string TheFirstTrack,

            [property: Bind("Track2")]
            string TheSecondTrack);

        [Fact]
        public void CanBindRecordWhenOnlyBlockIdIsSpecifiedAndActionContainsSingleItemAreSame()
        {
            object? ValueGetter(BindAttribute? bindAttribute)
            {
                return bindAttribute?.BlockId switch
                {
                    "Track1" => "Countdown to Armageddon",
                    "Track2" => "Bring The Noise",
                    _ => null
                };
            }
            var recordBinder = new RecordBinder();

            var success = recordBinder.TryBindRecord<ItTakesANationOfMillionsToHoldUsBack>(ValueGetter, out var bound);

            Assert.True(success);
            Assert.NotNull(bound);
            Assert.Equal("Countdown to Armageddon", bound.TheFirstTrack);
            Assert.Equal("Bring The Noise", bound.TheSecondTrack);
        }
    }
}
