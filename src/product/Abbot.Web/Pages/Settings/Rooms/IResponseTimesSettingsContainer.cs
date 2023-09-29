using Serious.Abbot.Pages.Settings.Organization;

namespace Serious.Abbot.Pages.Settings.Rooms;

/// <summary>
/// Interface for a page that can be used to set the response times for a room.
/// This is used by the _ResponseTimesForm partial view.
/// </summary>
public interface IResponseTimesSettingsContainer
{
    /// <summary>
    /// The current organizations.
    /// </summary>
    Entities.Organization Organization { get; }

    /// <summary>
    /// The <see cref="ResponseTimeSettings" /> to bind to.
    /// </summary>
    ResponseTimeSettings ResponseTimeSettings { get; set; }
}
