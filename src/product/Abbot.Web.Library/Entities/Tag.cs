using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

[DebuggerDisplay("{Name} ({Id})")]
public partial class Tag : TrackedEntityBase<Tag>, IOrganizationEntity, IAuditableEntity
{
    /// <summary>
    /// The tag name. Must be unique within the organization.
    /// </summary>
    [Column(TypeName = "citext")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// An optional description of the tag.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Returns <c>true</c> if this is a system tag, i.e. a tag that is created by the system such as part of our
    /// AI classifier.
    /// </summary>
    [NotMapped]
    public bool Generated => Name.Contains(':', StringComparison.Ordinal);

    /// <summary>
    /// The Id of the <see cref="Organization"/> this tag belongs to.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The <see cref="Organization"/> this tag belongs to.
    /// </summary>
    public Organization Organization { get; set; } = null!;

    /// <summary>
    /// The conversations this tag is applied to.
    /// </summary>
    public IList<ConversationTag> Conversations { get; set; } = null!;

    public AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        return new AuditEvent
        {
            Type = new("Tag", auditOperation),
            Description = $"{auditOperation} tag `{Name}`."
        };
    }

    static readonly Regex TagNameRegex = TagNameValidatorRegex();

    /// <summary>
    /// Whether the tag name is valid. Tag names follow the same rules as skill names.
    /// </summary>
    /// <param name="name">The tag name.</param>
    /// <param name="allowGenerated">Whether to allow generated tag names.</param>
    /// <returns><c>true</c> if the specified tag name is valid, otherwise <c>false</c></returns>
    public static bool IsValidTagName(string name, bool allowGenerated = false) => name is { Length: > 0 }
        && TagNameRegex.IsMatch(name) || allowGenerated && name.Split(':', 2).All(TagNameRegex.IsMatch);

    [GeneratedRegex(Skill.ValidNamePattern, RegexOptions.Compiled)]
    private static partial Regex TagNameValidatorRegex();
}

/// <summary>
/// A mapping of a <see cref="Tag"/> to a <see cref="Conversation"/>.
/// </summary>
public class ConversationTag
{
    /// <summary>
    /// The Id of the <see cref="Tag"/> applied to the <see cref="Conversation"/>.
    /// </summary>
    public int TagId { get; set; }

    /// <summary>
    /// The <see cref="Tag"/> applied to the <see cref="Conversation"/>.
    /// </summary>
    public Tag Tag { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="Conversation"/> the <see cref="Tag"/> is applied to.
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// The <see cref="Conversation"/> the <see cref="Tag"/> is applied to.
    /// </summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>
    /// The date this tag was created.
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// The <see cref="User"/> that assigned this tag to the conversation.
    /// </summary>
    public User Creator { get; set; } = null!;

    /// <summary>
    /// The Database Id of the <see cref="User"/> that assigned this tag to the conversation.
    /// </summary>
    public int CreatorId { get; set; }
}
