using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Xunit;

public class LocationTests
{
    public class TheToStringMethod
    {
        [Fact]
        public void ReturnsUndisclosedLocationWhenCoordinatesAndAddressNull()
        {
            var location = new Location(null, null, null);

            Assert.Equal("Undisclosed location.", location.ToString());
        }

        [Fact]
        public void ReturnsCoordinatesWhenFormattedAddressNull()
        {
            var coordinate = new Coordinate(42.1, 23.5);
            var location = new Location(coordinate, null, null);

            Assert.Equal("_(Latitude: `42.1`, Longitude: `23.5`)_.", location.ToString());
        }

        [Fact]
        public void ReturnsAddressAndCoordinates()
        {
            var coordinate = new Coordinate(42.1, 23.5);
            var location = new Location(coordinate, "My House", null);

            Assert.Equal("`My House` _(Latitude: `42.1`, Longitude: `23.5`)_.", location.ToString());
        }
    }
}
