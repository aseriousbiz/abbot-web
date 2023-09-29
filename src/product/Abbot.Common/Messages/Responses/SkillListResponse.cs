using System.Collections.Generic;

namespace Serious.Abbot.Messages;

/// <summary>
/// The response from calling https://api.ab.bot/api/cli/list. This is used by the Abbot CLI to
/// retrieve info about a skill and start an editing session.
/// </summary>
public class SkillListResponse
{
    /// <summary>
    /// The field the results are ordered by.
    /// </summary>
    public SkillOrderBy OrderBy { get; set; } = SkillOrderBy.Name;

    /// <summary>
    /// The direction the results are ordered by.
    /// </summary>
    public OrderDirection OrderDirection { get; set; } = OrderDirection.Ascending;

    /// <summary>
    /// The requested results.
    /// </summary>
    public IList<SkillGetResponse> Results { get; set; } = null!;
}
