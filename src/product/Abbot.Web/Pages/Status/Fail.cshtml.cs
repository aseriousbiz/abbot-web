using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serious.Abbot.Pages;

public class Fail : PageModel
{
    [SuppressMessage("Microsoft.Performance", "CA1822", Justification = "This is required by asp.net core.")]
    // ReSharper disable once UnusedMember.Global
    public void OnGet()
    {
        throw new InvalidOperationException("Chaos monkey at work!");
    }
}
