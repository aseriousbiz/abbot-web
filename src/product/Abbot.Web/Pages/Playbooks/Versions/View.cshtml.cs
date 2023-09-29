using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Repositories;
using Serious.Abbot.Web;
using Serious.Logging;

namespace Serious.Abbot.Pages.Playbooks.Versions;

[FeatureGate(FeatureFlags.Playbooks)]
public class ViewModel : UserPage
{
    static readonly ILogger<ViewModel> Log = ApplicationLoggerFactory.CreateLogger<ViewModel>();

    readonly PlaybookRepository _playbookRepository;

    public ViewModel(PlaybookRepository playbookRepository)
    {
        _playbookRepository = playbookRepository;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        return NotFound();
    }

    public async Task<IActionResult> OnGetExportAsync(string slug, int versionNumber, bool raw)
    {
        var version = await _playbookRepository.GetPlaybookVersionAsync(Organization, slug, versionNumber);
        if (version is null)
        {
            return Problems.NotFound("Playbook not found", "The playbook does not exist").ToActionResult();
        }

        using var playbookScope = Log.BeginPlaybookScope(version.Playbook);
        using var versionScope = Log.BeginPlaybookVersionScope(version);

        var json = version.SerializedDefinition;

        if (!raw)
        {
            json = FormatDefinition(json);
        }

        // Return the serialized description as JSON
        return new FileContentResult(
            Encoding.UTF8.GetBytes(json),
            "application/json")
        {
            FileDownloadName = $"playbook.{slug}_v{versionNumber}.json"
        };
    }

    static string FormatDefinition(string json)
    {
        try
        {
            var def = PlaybookFormat.Deserialize(json);
            return PlaybookFormat.Serialize(def, indented: true);
        }
        catch (Exception ex)
        {
            Log.ExportFormatFailed(ex);
            return json;
        }
    }
}

public static partial class ViewModelLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Failed to format Playbook export.")]
    public static partial void ExportFormatFailed(
        this ILogger<ViewModel> logger,
        Exception ex);
}
