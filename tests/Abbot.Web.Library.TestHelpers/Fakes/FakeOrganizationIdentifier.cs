using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.TestHelpers
{
    public class FakeOrganizationIdentifier : IOrganizationIdentifier
    {
        public FakeOrganizationIdentifier(IOrganizationIdentifier organizationIdentifier)
            : this(organizationIdentifier.PlatformId, organizationIdentifier.PlatformType)
        {
        }

        public FakeOrganizationIdentifier(
            string platformId = "T001",
            PlatformType platformType = PlatformType.Slack)
        {
            PlatformId = platformId;
            PlatformType = platformType;
        }

        public string PlatformId { get; }
        public PlatformType PlatformType { get; }
    }
}
