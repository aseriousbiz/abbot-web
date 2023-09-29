using Serious.Abbot.Entities;

namespace Serious.Abbot.Eventing.Entities;

public record MemberIds
{
    public required Id<Member> Id { get; init; }
    public required Id<Organization> OrganizationId { get; init; }

    public static implicit operator MemberIds(Member member) => new()
    {
        Id = member,
        OrganizationId = (Id<Organization>)member.OrganizationId,
    };
}
