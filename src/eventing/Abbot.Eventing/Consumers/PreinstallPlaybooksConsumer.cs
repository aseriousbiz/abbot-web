using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Eventing.Consumers;

public class PreinstallPlaybooksConsumer : IConsumer<OrganizationActivated>
{
    readonly IOrganizationRepository _organizationRepository;
    readonly PlaybookRepository _playbookRepository;
    readonly ILogger<PreinstallPlaybooksConsumer> _logger;

    public PreinstallPlaybooksConsumer(IOrganizationRepository organizationRepository,
        PlaybookRepository playbookRepository, ILogger<PreinstallPlaybooksConsumer> logger)
    {
        _organizationRepository = organizationRepository;
        _playbookRepository = playbookRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrganizationActivated> context)
    {
        var organization = context.GetPayload<Organization>();
        var abbot = await _organizationRepository.EnsureAbbotMember(organization);

        foreach (var preinstalled in PreinstalledPlaybook.ForNewOrganizations)
        {
            await PreinstallPlaybookAsync(organization, preinstalled, abbot);
        }
    }

    async Task PreinstallPlaybookAsync(Organization organization, PreinstalledPlaybook preinstalled, Member actor)
    {
        // Check if the playbook already exists
        var existingPlaybook = await _playbookRepository.GetBySlugAsync(preinstalled.Slug, organization);
        if (existingPlaybook is not null)
        {
            _logger.SkippingPreinstalledPlaybook(preinstalled.Slug);
            return;
        }

        var result = await _playbookRepository.CreateAsync(
            preinstalled.Name,
            preinstalled.Description,
            preinstalled.Slug,
            enabled: false,
            actor);

        if (result.Type == EntityResultType.Conflict)
        {
            _logger.SkippingPreinstalledPlaybook(preinstalled.Slug);
            return;
        }

        if (!result.IsSuccess)
        {
            _logger.ErrorCreatingPreinstalledPlaybook(preinstalled.Slug, result.ErrorMessage);
            return;
        }

        var playbook = result.Entity;

        _logger.CreatedPreinstalledPlaybook(playbook.Slug, playbook);

        // Add the definition as a published version
        var version = await _playbookRepository.CreateVersionAsync(
            playbook,
            preinstalled.SerializedDefinition,
            comment: "Preinstalled for you by Abbot.",
            actor);
        await _playbookRepository.SetPublishedVersionAsync(version, actor);

        _logger.CreatedPreinstalledPlaybookVersion(playbook.Slug, version.Version, version);
    }
}

public static partial class PreinstallPlaybooksConsumerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Created preinstalled playbook {Slug} with ID {PlaybookId}.")]
    public static partial void CreatedPreinstalledPlaybook(this ILogger<PreinstallPlaybooksConsumer> logger,
        string slug, Id<Playbook> playbookId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Created preinstalled playbook version {Slug} v{Version} with ID {PlaybookVersionId}.")]
    public static partial void CreatedPreinstalledPlaybookVersion(this ILogger<PreinstallPlaybooksConsumer> logger,
        string slug, int version, Id<PlaybookVersion> playbookVersionId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Skipping preinstalled playbook {Slug} because there is already a playbook with that slug.")]
    public static partial void SkippingPreinstalledPlaybook(this ILogger<PreinstallPlaybooksConsumer> logger,
        string slug);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Error creating preinstalled playbook {Slug}: {ErrorMessage}.")]
    public static partial void ErrorCreatingPreinstalledPlaybook(this ILogger<PreinstallPlaybooksConsumer> logger,
        string slug, string errorMessage);
}
