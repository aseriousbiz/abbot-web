using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Extensions;
using Serious.Abbot.Repositories;
using Serious.Filters;
#pragma warning disable CA1307

namespace Serious.Abbot.Entities.Filters;

/// <summary>
/// Class used to create filters for rooms.
/// </summary>
public static class RoomFilters
{
    static IReadOnlyDictionary<string, Func<IQueryable<Room>, Filter, IQueryable<Room>>> Create(AbbotContext db) =>
        new Dictionary<string, Func<IQueryable<Room>, Filter, IQueryable<Room>>>
        {
            ["room"] = (query, filter) => query.WhereOrNot(
                filter.Include,
                r => r.Name!.ToLower().Contains(filter.LowerCaseValue)
                     || r.PlatformRoomId.ToLower() == filter.LowerCaseValue),

            ["segment"] = (query, filter) => query.WhereOrNot(
                    filter.Include,
                    r => (filter.LowerCaseValue == "none" && (r.Customer == null || !r.Customer!.TagAssignments.Any())) || r.Customer!.TagAssignments.Any(a => a.Tag.Name.ToLower() == filter.LowerCaseValue)),

            ["customer"] = (query, filter) => query.WhereOrNot(
                filter.Include,
                r => (filter.LowerCaseValue == "none" && r.Customer == null) || r.Customer!.Name.ToLower() == filter.LowerCaseValue),

            ["integration"] = (query, filter) =>
                filter.LowerCaseValue switch {
                    "hubspot" => query.WhereOrNot(
                        filter.Include,
                        r => r.Links.Any(rl => rl.LinkType == RoomLinkType.HubSpotCompany)),
                    "zendesk" => query.WhereOrNot(
                        filter.Include,
                        r => r.Links.Any(rl => rl.LinkType == RoomLinkType.ZendeskOrganization)),
                    _ => query,
                },

            ["is"] = new RoomTrackStateFilter(db).Apply,
            ["fr"] = CreateRoomResponderFilter(RoomRole.FirstResponder),
            ["er"] = CreateRoomResponderFilter(RoomRole.EscalationResponder),
        };

    /// <summary>
    /// Creates and returns the set of room filters.
    /// </summary>
    /// <param name="db">An abbot context used for one of the filters.</param>
    /// <returns></returns>
    public static IEnumerable<IFilterItemQuery<Room>> CreateFilters(AbbotContext db) =>
        Create(db).Select(kvp => new FilterItemQuery<Room>(kvp.Key, kvp.Value));

    static Func<IQueryable<Room>, Filter, IQueryable<Room>> CreateRoomResponderFilter(RoomRole roomRole)
    {
        return (query, filter) => filter.LowerCaseValue switch {
            // Default responders are those who have no role assigned
            "default" => query.WhereOrNot(filter.Include, r => r.Assignments.All(ra => ra.Role != roomRole)),
            var loweredValue => query.WhereOrNot(
                filter.Include,
                r => r.Assignments
                    .Where(a => a.Role == roomRole)

                    .Any(a => a.Member.User.DisplayName.ToLower().Contains(loweredValue)
                              || a.Member.User.PlatformUserId.ToLower() == loweredValue)
            ),
        };
    }

    class RoomTrackStateFilter
    {
        readonly AbbotContext _db;

        public RoomTrackStateFilter(AbbotContext db)
        {
            _db = db;
        }

        public IQueryable<Room> Apply(IQueryable<Room> query, Filter filter)
            => Enum.TryParse<TrackStateFilter>(filter.Value, out var trackStateFilter)
                ? ApplyTrackedStateFilter(query, trackStateFilter)
                : query;

        IQueryable<Room> ApplyTrackedStateFilter(IQueryable<Room> query, TrackStateFilter trackedStateFilter)
        {
            // Supporting all combinations is Hard, so we've named a few
            if (trackedStateFilter is TrackStateFilter.All)
            {
                return query;
            }

            if (trackedStateFilter is TrackStateFilter.BotIsMember)
            {
                return ApplyActiveOnlyFilter(query)
                    .Where(r => r.BotIsMember == true);
            }

            var hubRoomIds = _db.Hubs.Select(h => h.RoomId);
            if (trackedStateFilter is TrackStateFilter.Hubs)
            {
                return ApplyActiveOnlyFilter(query)
                    .Where(r => r.BotIsMember == true)
                    .Where(r => hubRoomIds.Contains(r.Id));
            }

            return trackedStateFilter switch
            {
                TrackStateFilter.Tracked => ApplyActiveOnlyFilter(query)
                    .Where(r => !hubRoomIds.Contains(r.Id))
                    .Where(r => r.BotIsMember == true)
                    .Where(r => r.ManagedConversationsEnabled == true),
                TrackStateFilter.Untracked => ApplyActiveOnlyFilter(query)
                    .Where(r => !hubRoomIds.Contains(r.Id))
                    .Where(r => !r.ManagedConversationsEnabled)
                    .Where(r => r.BotIsMember == true),
                TrackStateFilter.Inactive => ApplyInactiveOnlyFilter(query),
                TrackStateFilter.BotMissing => ApplyActiveOnlyFilter(query)
                    .Where(r => r.BotIsMember != true),
                _ => query
            };
        }

        static IQueryable<Room> ApplyActiveOnlyFilter(IQueryable<Room> queryable)
        {
            return queryable.Where(r => r.Archived != true)
                .Where(r => r.Deleted != true)
                .OrderBy(r => r.Deleted == true
                    ? -1
                    : r.Archived == true
                        ? 1
                        : 0)
                .ThenBy(r => r.Name);
        }

        static IQueryable<Room> ApplyInactiveOnlyFilter(IQueryable<Room> queryable)
        {
            return queryable
                .Where(r => r.Archived == true || r.Deleted == true)
                .OrderBy(r => r.Name);
        }
    }
}
