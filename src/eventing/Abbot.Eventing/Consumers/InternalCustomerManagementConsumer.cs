using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Eventing.Consumers;

public class InternalCustomerManagementConsumer : IConsumer<OrganizationActivated>, IConsumer<OrganizationUpdated>, IConsumer<ResyncOrganizationCustomer>
{
    static readonly IReadOnlyDictionary<string, Func<Organization, string?>> MetadataFields =
        new Dictionary<string, Func<Organization, string?>>()
        {
            ["OrganizationId"] = o => $"{o.Id}",
            ["OrganizationPlatformId"] = o => o.PlatformId,
            ["OrganizationPlanType"] = o => o.PlanType.ToString(),
            ["OrganizationHasInstalledBot"] = o => o.IsBotInstalled().ToString(),
            ["OrganizationDomain"] = o => o.Domain,
            ["OrganizationSlug"] = o => o.Slug,
        };

    static readonly IReadOnlyDictionary<string, Func<Organization, bool>> SegmentPredicates =
        new Dictionary<string, Func<Organization, bool>>()
        {
            ["TrialActive"] = o => o.Trial is not null,
            ["TrialExpired"] = o => o is { PlanType: PlanType.Free, Trial: null },
            ["Subscribed"] = o => o is { PlanType: not PlanType.Free },
            ["FoundingCustomerPlan"] = o => o is { PlanType: PlanType.FoundingCustomer },
            ["UnlimitedPlan"] = o => o is { PlanType: PlanType.Unlimited },
            ["BusinessPlan"] = o => o is { PlanType: PlanType.Business },
            ["BotNotInstalled"] = o => !o.IsBotInstalled(),
        };

    readonly CustomerRepository _customerRepository;
    readonly IMetadataRepository _metadataRepository;
    readonly IOrganizationRepository _organizationRepository;
    readonly IOptions<AbbotOptions> _abbotOptions;
    readonly PlaybookDispatcher _playbookDispatcher;
    readonly ILogger<InternalCustomerManagementConsumer> _logger;

    public InternalCustomerManagementConsumer(
        CustomerRepository customerRepository,
        IMetadataRepository metadataRepository,
        IOrganizationRepository organizationRepository,
        IOptions<AbbotOptions> abbotOptions,
        PlaybookDispatcher playbookDispatcher,
        ILogger<InternalCustomerManagementConsumer> logger)
    {
        _customerRepository = customerRepository;
        _metadataRepository = metadataRepository;
        _organizationRepository = organizationRepository;
        _abbotOptions = abbotOptions;
        _playbookDispatcher = playbookDispatcher;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrganizationActivated> context)
    {
        var organization = context.GetPayload<Organization>();
        await UpdateOrganizationCustomerAsync(context, organization);
    }

    public async Task Consume(ConsumeContext<OrganizationUpdated> context)
    {
        var organization = context.GetPayload<Organization>();
        await UpdateOrganizationCustomerAsync(context, organization);
    }

    public async Task Consume(ConsumeContext<ResyncOrganizationCustomer> context)
    {
        var organization = context.GetPayload<Organization>();
        await UpdateOrganizationCustomerAsync(context, organization);
    }

    async Task UpdateOrganizationCustomerAsync(ConsumeContext context, Organization subject)
    {
        if (subject.IsSerious())
        {
            // If the subject org is one of our orgs, don't do anything.
            // We don't want to spam ourselves with updates to our own org and test orgs.
            return;
        }

        var seriousBiz = await _organizationRepository.GetAsync(WebConstants.ASeriousBizSlackId);
        if (seriousBiz is null)
        {
            _logger.CannotFindSeriousBiz(WebConstants.ASeriousBizSlackId);
            return;
        }

        var abbot = await _organizationRepository.EnsureAbbotMember(seriousBiz);

        // Ensure we have the required metadata fields.
        foreach (var (fieldName, _) in MetadataFields)
        {
            await EnsureMetadataFieldAsync(seriousBiz, abbot, fieldName);
        }

        var (customer, customerIsNew) = await EnsureCustomerAsync(seriousBiz, subject, abbot);

        // Update segments
        // (We ToList it because we're going to add new segments to the list when we create them)
        var allSegments = (await _customerRepository.GetAllCustomerSegmentsAsync(seriousBiz)).ToList();

        // Update customer segments
        var updatedSegments = new List<CustomerTag>(customer.TagAssignments.Select(a => a.Tag));
        foreach (var (segmentName, condition) in SegmentPredicates)
        {
            var segment = await EnsureSegmentExistsAsync(allSegments, seriousBiz, segmentName, abbot);
            if (condition(subject) && !updatedSegments.Contains(segment))
            {
                _logger.AddingRelevantSegment(segmentName, segment, customer);
                updatedSegments.Add(segment);
            }
            else if (!condition(subject) && updatedSegments.Contains(segment))
            {
                _logger.RemovingIrrelevantSegment(segmentName, segment, customer);
                updatedSegments.Remove(segment);
            }
        }

        _logger.ApplyingNewSegments(customer, string.Join(", ", updatedSegments.Select(s => s.Name)));
        await _customerRepository.AssignCustomerToSegmentsAsync(customer,
            updatedSegments.Select(s => (Id<CustomerTag>)s),
            abbot);

        if (customerIsNew)
        {
            // TODO: Refactor this to some common "Customer Created" MT consumer that can be used when a customer is created normally
            // But that's a problem for a company that survives the next month :).
            var outputs = new OutputsBuilder()
                .SetCustomer(customer)
                .Outputs;

            await _playbookDispatcher.DispatchAsync(
                CustomerCreatedTrigger.Id,
                outputs,
                seriousBiz,
                PlaybookRunRelatedEntities.From(customer));
        }
    }

    async Task<CustomerTag> EnsureSegmentExistsAsync(List<CustomerTag> allSegments, Organization seriousBiz,
        string segmentName, Member actor)
    {
        var segment = allSegments.SingleOrDefault(t => t.Name == segmentName);
        if (segment is null)
        {
            segment = await _customerRepository.CreateCustomerSegmentAsync(segmentName, actor, seriousBiz);
            allSegments.Add(segment);
        }

        return segment;
    }

    async Task<(Customer Customer, bool IsNew)> EnsureCustomerAsync(Organization seriousBiz, Organization subject, Member actor)
    {
        static string GetCustomerName(Organization organization) =>
            organization.Name ?? organization.Domain ?? organization.PlatformId;

        var matchingCustomers = await _customerRepository.GetCustomersByMetadataValueAsync(
            seriousBiz,
            "OrganizationPlatformId",
            subject.PlatformId);

        var customer = matchingCustomers.SingleOrDefault();
        var isNew = false;

        if (customer is null)
        {
            // Create a customer
            customer = await _customerRepository.CreateCustomerAsync(
                GetCustomerName(subject),
                Array.Empty<Room>(),
                actor,
                seriousBiz,
                null);

            isNew = true;

            await _organizationRepository.AssociateSeriousCustomerAsync(subject, customer, actor);
        }
        else if (customer.Name != subject.Name)
        {
            await _customerRepository.UpdateCustomerAsync(
                customer,
                GetCustomerName(subject),
                customer.Rooms,
                actor,
                seriousBiz);
        }

        // Associate metadata with them to link them to the internal organization.
        var updatedValues = MetadataFields
            .Select(p => new KeyValuePair<string, string?>(p.Key, p.Value(subject)))
            .ToDictionary(p => p.Key, p => p.Value);

        await _metadataRepository.UpdateCustomerMetadataAsync(customer,
            updatedValues,
            actor);

        return (customer, isNew);
    }

    async Task<MetadataField> EnsureMetadataFieldAsync(Organization seriousBiz, Member actor, string fieldName)
    {
        var field =
            await _metadataRepository.GetByNameAsync(MetadataFieldType.Customer, fieldName, seriousBiz);

        if (field is null)
        {
            _logger.CreatingMetadataField(fieldName, seriousBiz);
            field = await _metadataRepository.CreateMetadataFieldAsync(
                MetadataFieldType.Customer,
                fieldName,
                null,
                actor,
                seriousBiz);
        }

        return field;
    }
}

public static partial class InternalCustomerManagementConsumerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Cannot find A Serious Business in Organizations. Platform Id: {SeriousPlatformId}")]
    public static partial void CannotFindSeriousBiz(this ILogger<InternalCustomerManagementConsumer> logger,
        string seriousPlatformId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Creating metadata field {MetadataFieldName} for {OrganizationId}")]
    public static partial void CreatingMetadataField(this ILogger<InternalCustomerManagementConsumer> logger,
        string metadataFieldName, Id<Organization> organizationId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Created Customer {CustomerId} for Organization {OrganizationId}")]
    public static partial void CreatingOrganization(this ILogger<InternalCustomerManagementConsumer> logger,
        Id<Customer> customerId, Id<Organization> organizationId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Creating Customer Segment {SegmentName} for Organization {OrganizationId}")]
    public static partial void CreatingCustomerSegment(this ILogger<InternalCustomerManagementConsumer> logger,
        string segmentName, Id<Organization> organizationId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Adding now-relevant segment {SegmentName} ({SegmentId}) to {CustomerId}")]
    public static partial void AddingRelevantSegment(this ILogger<InternalCustomerManagementConsumer> logger,
        string segmentName, Id<CustomerTag> segmentId, Id<Customer> customerId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Removing irrelevant segment {SegmentName} ({SegmentId}) from {CustomerId}")]
    public static partial void RemovingIrrelevantSegment(this ILogger<InternalCustomerManagementConsumer> logger,
        string segmentName, Id<CustomerTag> segmentId, Id<Customer> customerId);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Applying new segments {SegmentIds} to {CustomerId}")]
    public static partial void ApplyingNewSegments(this ILogger<InternalCustomerManagementConsumer> logger,
        Id<Customer> customerId, string segmentIds);
}
