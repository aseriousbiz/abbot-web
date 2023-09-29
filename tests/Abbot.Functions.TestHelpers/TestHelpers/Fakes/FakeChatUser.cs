using NodaTime;
using Serious.Abbot;
using Serious.Abbot.Scripting;

namespace Serious.TestHelpers
{
    public class FakeChatUser : IChatUser
    {
        public FakeChatUser(string id, string username, string name)
        {
            Id = id;
            UserName = username;
            Name = name;
        }

        public string Id { get; set; }
        public string UserName { get; set; }
        public string Name { get; set; }
        public string? Email { get; set; }

        public WorkingHours? WorkingHours { get; }

        public DateTimeZone? TimeZone { get; set; }
        public ILocation Location { get; set; } = null!;
        public ChatAddress Address => new(ChatAddressType.User, Id);
    }
}
