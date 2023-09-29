using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using Serious.Abbot.AI;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Services;
using Serious.Cryptography;
using Serious.Slack;

namespace Serious.Abbot.Pages.Staff.Tools;

public class DumpsPage : StaffToolsPage
{
    readonly IOptions<AbbotOptions> _abbotOptions;

    public DumpsPage(
        IOptions<AbbotOptions> abbotOptions)
    {
        _abbotOptions = abbotOptions;
    }

    public required IReadOnlyList<DumpModel> Dumps { get; set; }
    public required string DumpPath { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        DumpPath = Path.Combine(_abbotOptions.Value.StaffAssetsPath, "dumps");

        try
        {
            Dumps = Directory.GetFiles(DumpPath)
                .Select(p => {
                    var file = new FileInfo(p);
                    return new DumpModel()
                    {
                        Name = file.Name,
                        SizeInBytes = file.Length,
                        CreationTimeUtc = file.CreationTimeUtc,
                        Url = $"/staff/assets/dumps/{file.Name}"
                    };
                }).ToList();
        }
        catch (DirectoryNotFoundException)
        {
            // It's fine, just no dumps yet.
            Dumps = Array.Empty<DumpModel>();
        }
        return Page();
    }
}

public record DumpModel
{
    public required string Name { get; init; }
    public required long SizeInBytes { get; init; }
    public required DateTime CreationTimeUtc { get; init; }

    public required string Url { get; set; }
}
