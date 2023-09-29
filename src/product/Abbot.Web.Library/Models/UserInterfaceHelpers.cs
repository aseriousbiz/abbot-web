using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Serious.Abbot.Models;

public static class UserInterfaceHelpers
{
    public static readonly IReadOnlyList<SelectListItem> AllWorkingHours = GenerateWorkingHours().ToList();

    static IEnumerable<SelectListItem> GenerateWorkingHours()
    {
        var time = TimeOnly.MinValue;

        // Yes, we could do this with a while loop.
        // But I feel more confident knowing that there's a fixed number of times this will run.
        // Other mechanisms could end up with an infinite loop unless done very carefully.
        // Ask me how I know. -anurse
        for (int i = 0; i < 48; i++)
        {
            yield return new SelectListItem(
                time.ToString("h:mm tt", CultureInfo.InvariantCulture),
                // We use 24hr time in the value to keep parsing unambiguous.
                time.ToString("HH:mm", CultureInfo.InvariantCulture));

            time = time.AddMinutes(30);
        }
    }
}
