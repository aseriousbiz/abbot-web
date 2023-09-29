using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Serialization;
using Serious.Abbot.Telemetry;
using Serious.AspNetCore;
using Serious.Logging;

namespace Serious.Abbot.Controllers;

/// <summary>
/// Handles incoming HTTP requests meant to trigger a skill.
/// </summary>
[ApiController]
[AllowAnonymous]
public class TriggerController : Controller
{
    static readonly ILogger<TriggerController> Log = ApplicationLoggerFactory.CreateLogger<TriggerController>();

    readonly ITriggerRepository _triggerRepository;
    readonly CustomerRepository _customerRepository;
    readonly PlaybookRepository _playbookRepository;
    readonly ISkillRunnerClient _skillRunnerClient;
    readonly IUrlGenerator _urlGenerator;
    readonly ISkillAuditLog _auditLog;
    readonly PlaybookDispatcher _dispatcher;

    /// <summary>
    /// Constructs a <see cref="TriggerController"/>.
    /// </summary>
    public TriggerController(
        ITriggerRepository triggerRepository,
        CustomerRepository customerRepository,
        PlaybookRepository playbookRepository,
        ISkillRunnerClient skillRunnerClient,
        IUrlGenerator urlGenerator,
        ISkillAuditLog auditLog,
        PlaybookDispatcher dispatcher)
    {
        _triggerRepository = triggerRepository;
        _customerRepository = customerRepository;
        _playbookRepository = playbookRepository;
        _skillRunnerClient = skillRunnerClient;
        _urlGenerator = urlGenerator;
        _auditLog = auditLog;
        _dispatcher = dispatcher;
    }

    [HttpPost, HttpPut, HttpOptions]
    [Route("/{skill}/trigger/{apiToken}")]
    [TriggerStandaloneHost]
    public async Task<IActionResult> OnRequestAsync(string skill, string apiToken)
    {
        Log.SkillMethodEntered(typeof(TriggerController), nameof(OnRequestAsync), null, skill);

        var trigger = await _triggerRepository.GetSkillHttpTriggerAsync(skill, apiToken);
        if (trigger is null)
        {
            return NotFound();
        }

        // Start an organization scope because we won't have had one otherwise since this isn't an authenticated endpoint.
        using var orgScope = Log.BeginOrganizationScope(trigger.Skill.Organization);
        using var skillScope = Log.BeginSkillScope(trigger.Skill);

        if (!trigger.Skill.Enabled)
        {
            return new StatusCodeResult(StatusCodes.Status404NotFound);
        }

        if (trigger.Skill.IsDeleted)
        {
            return new StatusCodeResult(StatusCodes.Status410Gone);
        }

        var request = await HttpTriggerRequest.CreateAsync(Request);
        var url = _urlGenerator.SkillPage(trigger.Skill.Name);
        var auditId = Guid.NewGuid();
        var response = await _skillRunnerClient.SendHttpTriggerAsync(trigger, request, url, auditId);
        var headers = response.Headers ?? new Dictionary<string, string?[]>();

        if (response.Headers is not null && response.Headers.Any())
        {
            foreach (var (key, values) in response.Headers)
            {
                if (values.Length is 0)
                {
                    continue;
                }

                Response.Headers[key] = values;
            }
        }

        var content = response is { Content: null } and { Success: false }
            ? AbbotJsonFormat.Default.Serialize(response.Errors, true)
            : response.Content;

        var result = new ContentResult
        {
            StatusCode = GetStatusCode(response),
            Content = content,
            ContentType = response.ContentType ?? GetContentType(Request)
        };

        await _auditLog.LogHttpTriggerRunEventAsync(trigger, request, headers, result, auditId);

        return result;
    }

    [HttpPost, HttpPut, HttpOptions]
    [TriggerSharedHost]
    [Route("api/internal/skills/{skill}/trigger/{apiToken}")]
    public Task<IActionResult> OnRequestLocalHostAsync(string skill, string apiToken)
    {
        return OnRequestAsync(skill, apiToken);
    }

    /// <summary>
    /// Handles incoming HTTP requests containing customer information meant to trigger a playbook with a
    /// <see cref="CustomerInfoSubmittedTrigger"/>.
    /// </summary>
    /// <param name="slug">The Playbook slug used to identify the playbook.</param>
    /// <param name="apiToken">The API token used to secure the trigger URL.</param>
    /// <returns></returns>
    [HttpPost, HttpPut, HttpOptions]
    [Route("/p/{slug}/customer/{apiToken}", Name = "playbook-customer-trigger")]
    [TriggerStandaloneHost]
    public async Task<IActionResult> OnPlaybookSubmitRequestAsync(string slug, string apiToken)
    {
        var playbook = await _triggerRepository.GetPlaybookFromTriggerTokenAsync(slug, apiToken);
        if (playbook is null)
        {
            return Problems.NotFound("Playbook not found.").ToActionResult();
        }

        if (!playbook.Organization.Enabled)
        {
            return Problems.OrganizationDisabled().ToActionResult();
        }

        if (!playbook.IsValidWebhookTriggerToken(apiToken))
        {
            return Problems.NotFound("Playbook not found.", "Playbook trigger token is not valid.").ToActionResult();
        }

        using var orgScope = Log.BeginOrganizationScope(playbook.Organization);

        var version = await _playbookRepository.GetCurrentVersionAsync(
            playbook,
            includeDraft: false,
            includeDisabled: false);

        if (version is null)
        {
            return Problems.NotFound("Playbook not found.", "Playbook is disabled or has no published versions.")
                .ToActionResult();
        }

        if (PlaybookFormat.Deserialize(version.SerializedDefinition) is not { } definition
            || PlaybookFormat.Validate(definition) is not [])
        {
            return Problems.InvalidPlaybook("Playbook definition is invalid").ToActionResult();
        }

        var triggerRequest = await HttpTriggerRequest.CreateAsync(Request);

        try
        {
            var relatedEntities = new PlaybookRunRelatedEntities();
            /*
             * Expected format: CustomerInfoSubmittedTriggerPayload
             *
             * {
             *   "customer": {
             *     "name": "The company name",
             *     "segments": ["segment1", "segment2"],
             *     "email": "contact@example.com",
             *   }
             * }
             */

            var customerInfoSubmitted = triggerRequest switch
            {
                { IsJson: true, RawBody.Length: > 0 } => AbbotJsonFormat.Default
                    .Deserialize<CustomerInfoSubmittedTriggerPayload>(triggerRequest.RawBody),
                { IsForm: true } => CustomerInfoSubmittedTriggerPayload.FromForm(triggerRequest.Form),
                _ => null
            };

            IDictionary<string, object?> outputs = new Dictionary<string, object?>();
            if (customerInfoSubmitted is not null)
            {
                var dbCustomer = await _customerRepository.GetCustomerByNameAsync(
                    customerInfoSubmitted.Customer.Name,
                    playbook.Organization);

                if (dbCustomer is not null)
                {
                    relatedEntities.Customer = dbCustomer;
                    outputs = new OutputsBuilder()
                        .SetCustomer(dbCustomer)
                        .Outputs;
                }
                else
                {
                    outputs["customer"] = customerInfoSubmitted.Customer;
                }
            }

            await _dispatcher.DispatchAsync(
                version,
                CustomerInfoSubmittedTrigger.Id,
                outputs,
                triggerRequest: triggerRequest);
        }
        catch (JsonException e)
        {
            return Problems.BadRequest("Error deserializing request body.", e.Message)
                .ToActionResult();
        }

        return triggerRequest.IsJson
            ? NoContent()
            : View("CustomerForm", Array.Empty<string>());
    }

    [HttpPost, HttpPut, HttpOptions]
    [TriggerSharedHost]
    [Route("api/internal/playbooks/{slug}/customer/{apiToken}", Name = "playbook-customer-trigger-local")]
    public Task<IActionResult> OnPlaybookSubmitRequestLocalHostAsync(string slug, string apiToken)
    {
        return OnPlaybookSubmitRequestAsync(slug, apiToken);
    }

    /// <summary>
    /// Handles incoming HTTP requests containing customer information meant to trigger a playbook with a
    /// <see cref="CustomerInfoSubmittedTrigger"/>.
    /// </summary>
    /// <param name="slug"></param>
    /// <param name="apiToken"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("/p/{slug}/customer/{apiToken}", Name = "playbook-customer-trigger")]
    [TriggerStandaloneHost]
    public async Task<IActionResult> OnPlaybookGetRequestAsync(string slug, string apiToken)
    {
        var playbook = await _triggerRepository.GetPlaybookFromTriggerTokenAsync(slug, apiToken);
        if (playbook is null)
        {
            return Problems.NotFound("Playbook not found.").ToActionResult();
        }

        if (!playbook.Organization.Enabled)
        {
            return Problems.OrganizationDisabled().ToActionResult();
        }

        if (!playbook.IsValidWebhookTriggerToken(apiToken))
        {
            return Problems.NotFound("Playbook not found.", "Playbook trigger token is not valid.").ToActionResult();
        }

        using var orgScope = Log.BeginOrganizationScope(playbook.Organization);

        var version = await _playbookRepository.GetCurrentVersionAsync(
            playbook,
            includeDraft: false,
            includeDisabled: false);

        if (version is null)
        {
            return Problems.NotFound("Playbook not found.", "Playbook is disabled or has no published versions.")
                .ToActionResult();
        }

        if (PlaybookFormat.Deserialize(version.SerializedDefinition) is not { } definition
            || PlaybookFormat.Validate(definition) is not [])
        {
            return Problems.InvalidPlaybook("Playbook definition is invalid").ToActionResult();
        }

        var segments = (await _customerRepository.GetAllCustomerSegmentsAsync(playbook.Organization))
            .Select(s => s.Name)
            .ToList();

        return View("CustomerForm", segments);
    }

    [HttpGet]
    [TriggerSharedHost]
    [Route("api/internal/playbooks/{slug}/customer/{apiToken}", Name = "playbook-customer-trigger-local")]
    public Task<IActionResult> OnPlaybookGetRequestLocalHostAsync(string slug, string apiToken)
    {
        return OnPlaybookGetRequestAsync(slug, apiToken);
    }

    static string GetContentType(HttpRequest request)
    {
        // Try and return the same content type that the client requests, if the client accepts it,
        // Otherwise fallback to application/xml or application/json depending on the content type of the request.
        return request.TryGetAcceptedContentType(out var contentType)
            ? contentType.MediaType.Value.Require()
            : request.IsXmlContentType() && request.AcceptsXml()
                ? "application/xml"
                : "application/json";
    }

    static int GetStatusCode(SkillRunResponse response)
    {
        return (response.Success, response.Content) switch
        {
            (true, { Length: > 0 }) => 200,
            (true, _) => 204,
            (false, _) => 500,
        };
    }
}
