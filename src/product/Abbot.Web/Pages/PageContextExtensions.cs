using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Serious.Abbot.Pages;

public static class PageContextExtensions
{
    static readonly object StaffToolsKey = new();

    public static bool InStaffTools(this ViewContext context) => context.HttpContext.InStaffTools();
    public static bool InStaffTools(this HttpContext context) =>
        context.Items.TryGetValue(StaffToolsKey, out var value) && value is true;

    public static void SetInStaffTools(this HttpContext context, bool inStaffTools) =>
        context.Items[StaffToolsKey] = inStaffTools;
}
