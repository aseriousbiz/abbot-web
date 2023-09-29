using System;
using NodaTime;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// A geo-coded location with timezone.
/// </summary>
public class Location : Geocode, ILocation, IEquatable<Location>
{
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public Location()
    {
    }

    public Location(ICoordinate? coordinate, string? formattedAddress, string? timeZoneId)
        : base(coordinate, formattedAddress)
    {
        TimeZone = GetTimeZoneById(timeZoneId);
    }

    /// <summary>
    /// The timezone for the location, if it was retrieved.
    /// </summary>
    public DateTimeZone? TimeZone { get; set; }

    static DateTimeZone? GetTimeZoneById(string? id) => id is null ? null : DateTimeZoneProviders.Tzdb.GetZoneOrNull(id);

    public override string ToString()
    {
        return this switch
        { { FormattedAddress: null, Coordinate: null } => "Undisclosed location.", { FormattedAddress: null, Coordinate: { } } => $"{Coordinate}.", { FormattedAddress: { }, Coordinate: { } } => $"`{FormattedAddress}` {Coordinate}.",
            _ => "Something strange is afoot."
        };
    }

    public bool Equals(Location? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Equals(Coordinate, other.Coordinate) &&
               FormattedAddress == other.FormattedAddress &&
               Equals(TimeZone, other.TimeZone);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((Location)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Coordinate, FormattedAddress, TimeZone);
    }
}
