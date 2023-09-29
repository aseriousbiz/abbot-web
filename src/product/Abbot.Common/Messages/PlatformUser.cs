using System;
using Newtonsoft.Json;
using NodaTime;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace Serious.Abbot.Models;

/// <summary>
/// Used to send information about a user to the skill runners. This is (and should be) a simple DTO.
/// </summary>
/// <remarks>
/// <para>
/// When Abbot calls a user written skill, it combines a Member and its User into an instance
/// of this. This class needs to be serializable and contain the information about a user that
/// a skill would need.
/// </para>
/// <para>
/// This is used to populate a ChatUser in .NET skills. This should not be confused with a
/// ChannelUser which is a user on an incoming chat message coming from a chat platform.
/// </para>
/// </remarks>
public class PlatformUser : IChatUser, IEquatable<PlatformUser>
{
    /// <summary>
    /// Constructs a <see cref="PlatformUser" />. This is needed for serialization.
    /// </summary>
    public PlatformUser()
    {
    }

    public PlatformUser(
        string id,
        string username,
        string name,
        string? email = null,
        string? timeZoneId = null,
        string? formattedAddress = null,
        double? latitude = null,
        double? longitude = null,
        WorkingHours? workingHours = null)
    {
        Id = id;
        UserName = username;
        Name = name;
        if (formattedAddress is not null || latitude is not null || longitude is not null || timeZoneId is not null)
        {
            var coordinate = latitude is not null && longitude is not null
                ? new Coordinate(latitude.Value, longitude.Value)
                : null;
            Location = new Location(coordinate, formattedAddress, timeZoneId);
        }
        Email = email;
        WorkingHours = workingHours;
    }

    /// <summary>
    /// The ID of the user on their chat platform.
    /// </summary>
    public string Id { get; init; } = null!;

    /// <summary>
    /// The username for the user on the platform.
    /// </summary>
    /// <remarks>
    /// Incoming Slack messages from Azure Bot Service only contain a username that we actually need to ignore
    /// because it's a deprecated field. Ugh.
    /// </remarks>
    public string UserName { get; init; } = null!;

    /// <summary>
    /// The display name for the user if known. Otherwise the username.
    /// </summary>
    /// <remarks>
    /// Incoming Slack messages from Azure Bot Service only contain a username that we actually need to ignore
    /// because it's a deprecated field. Ugh.
    /// </remarks>
    public string Name { get; init; } = null!;

    /// <summary>
    /// The Email of the user, if known.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// The location of the user, if they've told us.
    /// </summary>
    public ILocation? Location { get; init; }

    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public DateTimeZone? TimeZone => Location?.TimeZone;

    /// <summary>
    /// Gets the <see cref="ChatAddress"/> that can be used in the Reply API to send a message to this user.
    /// </summary>
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public ChatAddress Address => new(ChatAddressType.User, Id);

    public override string ToString() =>
        SlackFormatter.UserMentionSyntax(Id);

    public bool Equals(PlatformUser? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Id == other.Id && UserName == other.UserName && Name == other.Name && Email == other.Email && Equals(Location, other.Location);
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

        return Equals((PlatformUser)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, UserName, Name, Email, Location);
    }

    public WorkingHours? WorkingHours { get; init; }
}
