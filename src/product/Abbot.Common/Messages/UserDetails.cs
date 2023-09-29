using System.Collections.Generic;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

public class UserDetails : PlatformUser, IUserDetails
{
    public IDictionary<string, UserProfileField> CustomFields { get; }

    /// <summary>
    /// Constructs a <see cref="UserDetails" />. This is needed for serialization.
    /// </summary>
    public UserDetails()
    {
        CustomFields = new Dictionary<string, UserProfileField>();
    }

    public UserDetails(
        string id,
        string username,
        string name,
        string? email = null,
        string? timeZoneId = null,
        string? formattedAddress = null,
        double? latitude = null,
        double? longitude = null,
        WorkingHours? workingHours = null,
        IDictionary<string, UserProfileField>? customFields = null)
        : base(id, username, name, email, timeZoneId, formattedAddress, latitude, longitude, workingHours)
    {
        CustomFields = customFields ?? new Dictionary<string, UserProfileField>();
    }
}
