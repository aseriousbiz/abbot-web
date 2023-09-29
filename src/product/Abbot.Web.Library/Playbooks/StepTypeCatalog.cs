using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Playbooks;

public partial class StepTypeCatalog
{
    readonly FeatureService _featureService;
    readonly IIntegrationRepository _integrationRepository;

    // The regex pattern for a step type name.
    // A step type name MUST start with a lowercase ASCII letter (a-z)
    // The rest of the name MUST consist of lowercase ASCII letters (a-z), digits (0-9), hyphens (-), periods (.), and colons (:).
    // Notably: Underscores and uppercase ASCII are NOT PERMITTED.
    // We can relax these rules if we need to.
    //
    // Essentially:
    // * Use lower-case words
    // * Separate words with '-'
    // * Separate categories/namespaces with '.' and ':' (e.g. 'system.webhook' or 'integration:zendesk.post-message')
    [GeneratedRegex("""^[a-z]([a-z0-9\.:-]*)$""")]
    private static partial Regex StepNamePattern();

    readonly List<StepType> _stepTypes;
    readonly Dictionary<string, StepType> _stepTypesByName;
    readonly Dictionary<string, ITriggerType> _triggerTypesByTypeId;

    public StepTypeCatalog(IEnumerable<ITriggerType> triggerTypes, IEnumerable<IActionType> actionTypes, FeatureService featureService, IIntegrationRepository integrationRepository)
    {
        _featureService = featureService;
        _integrationRepository = integrationRepository;
        _triggerTypesByTypeId = triggerTypes.ToDictionary(t => t.Type.Name);

        _stepTypes = _triggerTypesByTypeId.Values.Select(t => t.Type)
            .Concat(actionTypes.Select(t => t.Type))
            .ToList();

        _stepTypesByName = _stepTypes.ToDictionary(t => t.Name);
        EnsureStepTypeNamesAreValid();
    }

    void EnsureStepTypeNamesAreValid()
    {
        // Validate step type names match the expected pattern.
        var errors = new List<PlaybookDiagnostic>();
        foreach (var stepType in _stepTypes)
        {
            errors.AddRange(PlaybookFormat.Validate(stepType));
        }

        if (errors.Count > 0)
        {
            throw new UnreachableException(
                $"Step type validation failed:\n{string.Join("\n", errors)}");
        }
    }

    public async Task<StepTypeList> GetAllTypesAsync(Organization organization, IFeatureActor featureActor)
    {
        var allTypes = _stepTypes;

        // Check all the feature flags mentioned by step types.
        var activeFeatureFlags = new List<string>();
        foreach (var flag in allTypes.SelectMany(t => t.RequiredFeatureFlags).Distinct())
        {
            if (await _featureService.IsEnabledAsync(flag, featureActor))
            {
                activeFeatureFlags.Add(flag);
            }
        }

        var enabledIntegrations = (await _integrationRepository.GetIntegrationsAsync(organization))
            .Where(i => i.Enabled)
            .Select(i => i.Type)
            .ToList();

        if (organization.IsBotInstalled() && enabledIntegrations.All(i => i != IntegrationType.SlackApp))
        {
            enabledIntegrations.Add(IntegrationType.SlackApp);
        }

        return new()
        {
            StepTypes = allTypes,
            ActiveFeatureFlags = activeFeatureFlags,
            EnabledIntegrations = enabledIntegrations,
        };
    }

    public bool TryGetType(string name, [MaybeNullWhen(false)] out StepType type) =>
        _stepTypesByName.TryGetValue(name, out type);

    public bool TryGetTriggerType(string name, [MaybeNullWhen(false)] out ITriggerType type)
        => _triggerTypesByTypeId.TryGetValue(name, out type);
}

public class StepTypeList
{
    /// <summary>
    /// Gets all the step types known to the system, INCLUDING those that are not visible to the current user because of staff mode and feature flags.
    /// </summary>
    public required IReadOnlyList<StepType> StepTypes { get; init; }

    /// <summary>
    /// Gets a list of all the feature flags referenced by step types that are active for the current user.
    /// </summary>
    public required IReadOnlyList<string> ActiveFeatureFlags { get; init; }

    /// <summary>
    /// Gets a list of all the integrations enabled for the organization.
    /// </summary>
    public required IReadOnlyList<IntegrationType> EnabledIntegrations { get; init; }
}
