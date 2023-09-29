using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Extensions;
using Serious.Abbot.Forms;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.AspNetCore;
using Serious.AspNetCore.DataAnnotations;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Pages.Staff.Organizations.Forms;

public class EditModel : OrganizationDetailPage
{
    readonly IFormsRepository _formsRepository;
    readonly ISettingsManager _settingsManager;
    readonly ISlackApiClient _slackApiClient;

    public bool IsDefault { get; set; }

    public string Key { get; set; } = null!;

    [BindProperty]
    public bool IsEnabled { get; set; }

    [Required]
    [BindProperty]
    public string? Definition { get; set; }

    [Display(Name = "System Definition")]
    public string? SystemDefinition { get; set; }

    [BindProperty]
    [Display(Name = "Hubspot Form Guid", Description = "Specify a Hubspot Form Guid if creating a ticket by posting to a Hubspot form")]
    public string? FormGuid { get; set; }

    [BindProperty]
    [RequiredIf(nameof(FormGuid))]
    [Display(Name = "Token Form Field", Description = "Specify a Hubspot Form Field we'll use to store a token used to associate the Hubspot form submission with the ticket")]
    public string? TokenFormField { get; set; }

    public EditModel(
        AbbotContext db,
        IAuditLog auditLog,
        IFormsRepository formsRepository,
        ISettingsManager settingsManager,
        ISlackApiClient slackApiClient) : base(db, auditLog)
    {
        _formsRepository = formsRepository;
        _settingsManager = settingsManager;
        _slackApiClient = slackApiClient;
    }

    public async Task OnGet(string id, string form)
    {
        Key = form;

        await InitializeDataAsync(id);

        var customForm = await _formsRepository.GetFormAsync(Organization, form);

        if (await _settingsManager.GetHubSpotFormSettingsAsync(Organization) is { } formSettings)
        {
            (FormGuid, TokenFormField) = formSettings;
        }

        SystemDefinition = SystemForms.Definitions.TryGetValue(form, out var systemDefinition)
            ? FormEngine.SerializeFormDefinition(systemDefinition, indented: true)
            : null;

        if (customForm is not null)
        {
            IsEnabled = customForm.Enabled;
            IsDefault = false;

            // This is a little silly, but it serves to reformat the JSON so it's easier to edit
            Definition = FormEngine.SerializeFormDefinition(FormEngine.DeserializeFormDefinition(customForm.Definition), indented: true);
        }
        else
        {
            // Have the "Enabled" checkbox be unchecked by default for default forms
            // so that if a staff member creates a custom form it will remain disabled until they enable it.
            IsEnabled = false;
            IsDefault = true;
            Definition = SystemDefinition;
        }
    }

    public async Task<IActionResult> OnPostAsync(string id, string form)
    {
        Key = form;
        await InitializeDataAsync(id);

        FormDefinition definition;
        try
        {
            definition = FormEngine.DeserializeFormDefinition(Definition.Require());
        }
        catch (JsonException jex)
        {
            ModelState.AddModelError(nameof(Definition), jex.Message);
            return Page();
        }

        var optionContainsSemicolon = definition
            .Fields
            .SelectMany(o => o.Options)
            .Any(o => o.Value.Contains(HubSpotLinker.HubSpotValueDelimiter, StringComparison.Ordinal));

        if (optionContainsSemicolon)
        {
            ModelState.AddModelError(nameof(Definition), "Option value may not contain a semicolon ; as that would break multi-valued fields.");
            return Page();
        }

        // We re-serialize to ensure that any extra fields posted on the original Definition are removed.
        var serializedDefinition = FormEngine.SerializeFormDefinition(definition, indented: false);

        if (IsEnabled && FormGuid is not null && TokenFormField is not null)
        {
            await _settingsManager.SetHubSpotFormSettingsAsync(
                new FormSettings(FormGuid, TokenFormField),
                Organization,
                Viewer.User);
        }
        else
        {
            FormGuid = null;
            TokenFormField = null;
            // We have to remove these settings if the form is not enabled because the existence of these fields
            // causes us to use the Hubspot Form API, and that's surprising if the custom form is disabled.
            // It would be nice to keep these values around, but we'll figure that out later - haacked
            await _settingsManager.RemoveHubSpotFormSettingsAsync(Organization, Viewer.User);
        }

        var currentForm = await _formsRepository.GetFormAsync(Organization, form);
        if (currentForm is not null)
        {
            currentForm.Enabled = IsEnabled;
            currentForm.Definition = serializedDefinition;

            await _formsRepository.SaveFormAsync(currentForm, Viewer);
        }
        else
        {
            await _formsRepository.CreateFormAsync(Organization, form, serializedDefinition, IsEnabled, Viewer);
        }

        StatusMessage = "Form saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id, string form)
    {
        Key = form;
        await InitializeDataAsync(id);

        var currentForm = await _formsRepository.GetFormAsync(Organization, form);
        await _settingsManager.RemoveHubSpotFormSettingsAsync(Organization, Viewer.User);
        if (currentForm is not null)
        {
            await _formsRepository.DeleteFormAsync(currentForm, Viewer);
            StatusMessage = "Custom form deleted. Reverted to default form.";
        }
        else
        {
            StatusMessage = "Custom form not found.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestAsync(string id, string form)
    {
        await InitializeDataAsync(id);

        FormDefinition definition;
        try
        {
            definition = FormEngine.DeserializeFormDefinition(Definition.Require());
        }
        catch (JsonException jex)
        {
            ModelState.AddModelError(nameof(Definition), jex.Message);
            return Page();
        }

        // Stash this form in a temporary setting
        var serializedDefinition = FormEngine.SerializeFormDefinition(definition, indented: false);
        var settingName = $"FormTest:{Guid.NewGuid():N}";
        await _settingsManager.SetAsync(
            SettingsScope.Member(Viewer),
            settingName,
            serializedDefinition,
            Viewer.User,
            TimeSpan.FromMinutes(5));

        // Send a message to the user that includes a button to test the form.
        await _slackApiClient.SendDirectMessageAsync(
            Viewer.Organization,
            Viewer.User,
            "Click the button below to test the form.",
            new Section("Click the button below to test the form."),
            new Actions(
                InteractionCallbackInfo.For<FormHandler>(),
                new ButtonElement("Test Form", settingName)
                {
                    ActionId = "test_form",
                }));

        var message = "Sent you a DM that will allow you to test the form.";

        // Ideally, this will be a turbo request.
        // If it is, we'll just use Turbo Streams to set the Status Message.
        // That _also_ has the desirable side effect of not doing a Redirect.
        // The redirect will wipe out the form the user is editing, so we want to avoid that.
        if (Request.IsTurboRequest())
        {
            return TurboFlash(message);
        }

        StatusMessage = message;
        return RedirectToPage();
    }

    protected override Task InitializeDataAsync(Organization organization)
    {
        return Task.CompletedTask;
    }
}
