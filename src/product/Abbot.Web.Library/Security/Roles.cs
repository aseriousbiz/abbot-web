using System.Collections.Generic;
using System.Linq;

namespace Serious.Abbot.Security;

public static class Roles
{
    public const string Agent = nameof(Agent);
    public const string Administrator = nameof(Administrator);
    public const string Staff = nameof(Staff);
    public static readonly IReadOnlyList<string> All = new[] { Agent, Administrator, Staff };
    public static readonly IReadOnlyList<string> AllExceptStaff = All.Except(new[] { Staff }).ToList();
}
