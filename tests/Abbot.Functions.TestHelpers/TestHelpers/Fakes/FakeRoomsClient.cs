using Serious.Abbot.Functions.Models;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.TestHelpers
{
    public class FakeRoomsClient : IRoomsClient
    {
        public Task<IResult<IRoomInfo>> CreateAsync(string name, bool isPrivate)
        {
            throw new NotImplementedException();
        }

        public Task<IResult> ArchiveAsync(IRoomMessageTarget room)
        {
            throw new NotImplementedException();
        }

        public Task<IResult> UnarchiveAsync(IRoomMessageTarget room)
        {
            throw new NotImplementedException();
        }

        public Task<IResult> InviteUsersAsync(IRoomMessageTarget room, IEnumerable<IChatUser> users)
        {
            throw new NotImplementedException();
        }

        public Task<IResult> SetTopicAsync(IRoomMessageTarget room, string topic)
        {
            throw new NotImplementedException();
        }

        public Task<IResult> SetPurposeAsync(IRoomMessageTarget room, string purpose)
        {
            throw new NotImplementedException();
        }

        public Task<AbbotResponse> UpdateMetadataAsync(IRoomMessageTarget room, IReadOnlyDictionary<string, string?> metadata)
        {
            throw new NotImplementedException();
        }

        public IRoomMessageTarget GetTarget(string id) => new RoomMessageTarget(id);
        public Task<IResult<IRoomDetails>> GetDetailsAsync(IRoomMessageTarget room)
        {
            throw new NotImplementedException();
        }

        public Task<IResult<IReadOnlyList<WorkingHours>>> GetCoverageAsync(IRoomMessageTarget room, RoomRole roomRole, string? timeZoneId)
        {
            throw new NotImplementedException();
        }

        public Task<AbbotResponse> NotifyAsync(RoomNotification notification, IRoomMessageTarget room)
        {
            throw new NotImplementedException();
        }
    }
}
