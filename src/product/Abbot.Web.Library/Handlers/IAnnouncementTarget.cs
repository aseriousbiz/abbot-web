using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// A target for an announcement such as All Rooms, Selected Rooms, or Customer Segments.
/// </summary>
public interface IAnnouncementTarget
{
    /// <summary>
    /// Gets the label used to describe the target for use in the success message.
    /// </summary>
    /// <param name="announcement"></param>
    /// <returns></returns>
    string GetSuccessMessageLabel(Announcement announcement);

    /// <summary>
    /// Resolve the rooms for the announcement.
    /// </summary>
    /// <param name="announcement">The announcement.</param>
    /// <returns></returns>
    Task<IReadOnlyList<AnnouncementMessage>> ResolveAnnouncementRoomsAsync(Announcement announcement);

    /// <summary>
    /// Returns true if this target is the target for the announcement.
    /// </summary>
    /// <param name="announcement">The announcement.</param>
    /// <returns><c>true</c> if this target is the target for the announcement.</returns>
    bool IsTargetForAnnouncement(Announcement announcement);

    /// <summary>
    /// Returns true if this target is the selected target in the Create Announcement dialog.
    /// </summary>
    /// <returns><c>true</c> if this target is the target for the state of the dialog.</returns>
    /// <returns></returns>
    bool IsSelectedTarget(string? targetName);

    /// <summary>
    /// Updates the <paramref name="announcement"/> with the target information from the announcement dialog selection.
    /// </summary>
    /// <param name="viewContext">The view context.</param>
    /// <param name="state">The current state.</param>
    /// <param name="announcement">The announcement to update.</param>
    /// <returns><c>true</c> if the announcement update is successful.</returns>
    Task<bool> HandleTargetSelectedAsync(
        IViewContext<IViewSubmissionPayload> viewContext,
        BlockActionsState state,
        Announcement announcement);
}

public static class AnnouncementTargetRegistration
{
    public static IServiceCollection AddAnnouncementDispatcherAndTargets(this IServiceCollection services)
    {
        services.AddTransient<AnnouncementDispatcher>();
        services.AddTransient<IAnnouncementTarget, AllRoomsAnnouncementTarget>();
        services.AddTransient<IAnnouncementTarget, SelectedRoomsAnnouncementTarget>();
        services.AddTransient<IAnnouncementTarget, CustomerSegmentsAnnouncementTarget>();

        return services;
    }
}
