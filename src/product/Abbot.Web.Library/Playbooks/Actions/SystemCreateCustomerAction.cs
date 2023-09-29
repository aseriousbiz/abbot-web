using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Playbooks.Actions;

/// <summary>
/// A Playbook action that creates a customer in our system based off a webhook.
/// </summary>
public class SystemCreateCustomerAction : ActionType<SystemCreateCustomerAction.Executor>
{
    public override StepType Type { get; } = new("system.create-customer", StepKind.Action)
    {
        Category = "system",
        Presentation = new()
        {
            Label = "Create customer",
            Icon = "fa-user",
            Description = "Creates a Customer in Abbot using information from the \"Customer Info Submitted\" trigger.",
        },
        Inputs =
        {
            new("customer", "Customer", PropertyType.Customer)
            {
                Hidden = true,
                Default = "{{ trigger.outputs.customer }}"
            },
            new("continue_if_exists", "Continue if customer exists", PropertyType.Boolean)
            {
                Description = "If checked, the step will continue with the existing customer if it already exists."
            }
        },
        Outputs =
        {
            new("customer", "Customer", PropertyType.Customer)
            {
                Description = "The created or existing customer.",
            },
        }
    };
    public class Executor : IActionExecutor
    {
        readonly CustomerRepository _customerRepository;
        readonly IUserRepository _userRepository;

        public Executor(CustomerRepository customerRepository, IUserRepository userRepository)
        {
            _customerRepository = customerRepository;
            _userRepository = userRepository;
        }

        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            var outputsBuilder = new OutputsBuilder();
            var organization = context.Playbook.Organization;
            var customer = context.Expect<SubmittedCustomerInfo>("customer");
            var continueIfExists = context.Get<bool>("continue_if_exists");
            if (customer.Name is not { Length: > 0 } customerName)
            {
                return new StepResult(StepOutcome.Failed)
                {
                    Problem = Problems.ArgumentError("customer", "Customer must be provided in the webhook payload like so {\"customer\":{\"name\":\"Customer Name\"}}.")
                };
            }

            var existingCustomer = await _customerRepository.GetCustomerByNameAsync(customerName, organization);

            if (!continueIfExists && existingCustomer is not null)
            {
                return new StepResult(StepOutcome.Failed)
                {
                    Problem = Problems.ArgumentError("customer", $"The customer \"{existingCustomer.Name}\" already exists."),
                };
            }

            var notices = new List<Notice>();

            if (existingCustomer is not null)
            {
                notices.Add(new Notice(
                    NoticeType.Warning,
                    $"The customer \"{existingCustomer}\" already exists. Continuing with the existing customer."));
            }
            else
            {
                var invalidSegmentNames = customer
                    .Segments
                    .Where(segmentName => !Tag.IsValidTagName(segmentName, allowGenerated: false))
                    .ToList();
                if (invalidSegmentNames.Any())
                {
                    return new StepResult(StepOutcome.Failed)
                    {
                        Problem = Problems.ArgumentError(
                            "customer.segments",
                            $"Invalid segment name(s): {string.Join(", ", invalidSegmentNames)}."),
                    };
                }

                var actor = await _userRepository.EnsureAbbotMemberAsync(organization);

                existingCustomer = await _customerRepository.CreateCustomerAsync(
                    customerName,
                    Enumerable.Empty<Room>(),
                    actor,
                    organization,
                    customer.Email);

                string details;
                if (customer.Segments is { Count: > 0 } segments)
                {
                    var result = await _customerRepository.GetOrCreateSegmentsByNamesAsync(
                        segments,
                        actor,
                        organization);
                    await _customerRepository.AssignCustomerToSegmentsAsync(
                        existingCustomer,
                        result.Select(t => new Id<CustomerTag>(t.Id)),
                        actor);

                    details = $"With segments: {string.Join(", ", segments)}";
                }
                else
                {
                    details = "No segments were provided.";
                }

                notices.Add(new Notice(
                    NoticeType.Information,
                    $"Created customer \"{existingCustomer.Name}\".",
                    details));
            }

            outputsBuilder.SetCustomer(existingCustomer);
            return new StepResult(StepOutcome.Succeeded)
            {
                Outputs = outputsBuilder.Outputs,
                Notices = notices,
            };
        }
    }
}
