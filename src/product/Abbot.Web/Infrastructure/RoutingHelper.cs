using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Serious.Abbot.Infrastructure;

public static class RoutingHelper
{
    public static string LowercaseRouteTemplate(string template)
    {
        var start = 0;
        var output = new StringBuilder();
        while (start < template.Length)
        {
            var nextBrace = template.IndexOf('{', start);
            var end = nextBrace < 0
                ? template.Length
                : nextBrace;

            // We explicitly want lowercase here.
            output.Append(template[start..end].ToLowerInvariant());

            if (nextBrace >= 0)
            {
                var endBrace = template.IndexOf('}', nextBrace);
                output.Append(template[nextBrace..(endBrace + 1)]);
                start = endBrace + 1;
            }
            else
            {
                start = template.Length;
            }
        }

        return output.ToString();
    }

    public static void ApplyConstraintToAllInstancesOfToken(IEnumerable<SelectorModel> selectors, IEnumerable<string> tokenNames, string constraint)
    {
        foreach (var selector in selectors)
        {
            if (selector.AttributeRouteModel is { Template: { Length: > 0 } template })
            {
                foreach (var tokenName in tokenNames)
                {
                    template = template
                        .Replace($"{{{tokenName}}}", $"{{{tokenName}:{constraint}}}", StringComparison.OrdinalIgnoreCase)
                        .Replace($"{{{tokenName}?}}", $"{{{tokenName}:{constraint}?}}", StringComparison.OrdinalIgnoreCase);
                }
                selector.AttributeRouteModel.Template = template;
            }
        }
    }
}
