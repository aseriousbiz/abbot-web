using System;
using Microsoft.AspNetCore.Http;

namespace Serious.Abbot.Extensions;

public static class PathStringExtensions
{
    public static bool IsSamePath(this PathString pathString, PathString compare)
    {
        return pathString.Equals(compare, StringComparison.OrdinalIgnoreCase)
               || (pathString + "/").Equals(compare, StringComparison.OrdinalIgnoreCase);
    }
}
