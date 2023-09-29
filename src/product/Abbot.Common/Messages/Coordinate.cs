using System;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// Represents a geographic coordinate.
/// </summary>
public class Coordinate : ICoordinate, IEquatable<Coordinate>
{
    public Coordinate()
    {
    }

    public Coordinate(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// The latitude. Those are the lines that are the belt of the earth.
    /// </summary>
    public double Latitude { get; init; }

    /// <summary>
    /// The longitude. Those are the pin stripes of the earth.
    /// </summary>
    public double Longitude { get; init; }

    public void Deconstruct(out double latitude, out double longitude)
    {
        latitude = Latitude;
        longitude = Longitude;
    }

    public override string ToString()
    {
        return $"_(Latitude: `{Latitude}`, Longitude: `{Longitude}`)_";
    }

    public bool Equals(Coordinate? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude);
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

        return Equals((Coordinate)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Latitude, Longitude);
    }
}
