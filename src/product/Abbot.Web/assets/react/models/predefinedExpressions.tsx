import {PlaybookDefinition} from "./playbookDefinition";
import {PlacedStep, Step} from "./step";
import {ComparisonTypeOption} from "./comparisonType";
import {TextInput} from "../components/propertyEditors/formControls";
import * as React from "react";
import {ExpressionOption, ExtendedOption, Option} from "./options";
import Select from "react-select";
import {SegmentsDropDown} from "../components/propertyEditors/segmentsDropDown";
import {ChannelMultiSelect, CustomerMultiSelect} from "../components/propertyEditors/fetchMultiSelect";
import {StepPropertyProps} from "../components/propertyEditors/propertyEditor";
import usePlaybook from "../hooks/usePlaybooks";

/* Type for a method that checks if the predefined expression is available and returns the step that made it available */
export type GetSourceStepMethod = (playbook: PlaybookDefinition, currentStep: Step, inRichTextEditor?: boolean) => PlacedStep | undefined;

// This allows an expression to override the label or description for the expression option.
export type GetPresentationMethod = (sourceStep?: PlacedStep, inRichTextEditor?: boolean) => ExpressionPresentation | null;

export type ExpressionInputProps = StepPropertyProps<string | readonly Option[]>;

export interface ExpressionPresentation {
    label?: string;
    description?: string;
}

const defaultComparisonTypeOptions = [
    {value: 'StartsWith', label: 'Starts with'},
    {value: 'EndsWith', label: 'Ends with'},
    {value: 'Contains', label: 'Contains'},
    {value: 'RegularExpression', label: 'Regular expression'},
    {value: 'ExactMatch', label: 'Exact match'},
    {value: 'Exists', label: 'Exists', title: 'Is true if the value exists.'},
];

export class HandlebarExpression {
    public getSourceStep: GetSourceStepMethod;
    public getPresentation: GetPresentationMethod;
    public relevantComparisonTypes: ComparisonTypeOption[];
    public readonly targetOutput?: string; // The output that this expression can fill in for.
    public readonly Input: React.ComponentType<ExpressionInputProps>

    constructor(public expression: string,
                name: string,
                options?: {
                    getSourceStep?: GetSourceStepMethod,
                    comparisonTypeOptions?: ComparisonTypeOption[],
                    Input?: React.ComponentType<ExpressionInputProps>,
                    targetOutput?: string,
                    getPresentation?: GetPresentationMethod,
                }
        ) {

        options = options || {};

        this.getSourceStep = options.getSourceStep || (() => undefined);
        this.relevantComparisonTypes = options.comparisonTypeOptions || defaultComparisonTypeOptions;
        this.Input = options.Input || DefaultExpressionInput;
        this.targetOutput = options.targetOutput;

        const defaultGetPresentation = (sourceStep?: PlacedStep): ExpressionPresentation => {
            const context = PredefinedExpressions.formatContext(this, sourceStep);
            return {
                label: name,
                description: context
            };
        };

        this.getPresentation = options.getPresentation
            ? ((sourceStep, inRichTextEditor) => {
                // We'll fill in missing values if possible so that implementations
                // only need to override what they need to.
                const presentation = options.getPresentation(sourceStep, inRichTextEditor)
                    ?? defaultGetPresentation(sourceStep);
                const description = presentation.description
                    ? presentation.description
                    : PredefinedExpressions.formatContext(this, sourceStep);
                return {
                    label: presentation.label ?? name,
                    description,
                }
            })
            : defaultGetPresentation;
    }

    get value() {
        return `{{ ${this.expression} }}`;
    }
}

function DefaultExpressionInput({ property, onChange, value }: ExpressionInputProps) {
    if (typeof value !== 'string') {
        value = '';
    }
    return (
        <TextInput type="text"
            placeholder={property.placeholder}
            name={property.name}
            onChange={onChange}
            required={!!property.required}
            value={value} />
    )
}

export class PredefinedExpressions {
    public static get all() : HandlebarExpression[] {
        return Object.values(predefinedExpressionsLookup);
    }

    // TODO: We may want to make this generic later, but for now this is fine.
    public static getMemberPropertyExpressions() : HandlebarExpression[] {
        return [
        ];
    }

    public static getChannelPropertyExpressions() : HandlebarExpression[] {
        return [
            predefinedExpressionsLookup.channel,
        ];
    }

    public static getMessageTargetPropertyExpressions(): HandlebarExpression[] {
        return [
            predefinedExpressionsLookup.message_url,
            ...this.getChannelPropertyExpressions(),
        ];
    }

    public static getByName(name: string): HandlebarExpression {
        return predefinedExpressionsLookup[name];
    }

    /* Given an expression value such as `{{ outputs.channel.id }}` we'll return the predefined expression.
    *  Also works for `outputs.channel.id` */
    public static getByValueExpression(expression: string): HandlebarExpression | undefined {
        if (!expression) {
            return undefined;
        }
        expression = expression.trim();

        expression = expression.startsWith('{{') && expression.endsWith('}}')
            ? expression.substring(2, expression.length - 2).trim()
            : expression;
        return PredefinedExpressions.all.find(e => e.expression === expression);
    }

    public static getExpressionOption(value: string, playbook: PlaybookDefinition, step: PlacedStep) : ExtendedOption | undefined {
        const expression = PredefinedExpressions.getByValueExpression(value);
        const sourceStep = expression?.getSourceStep(playbook, step);
        if (expression) {
            const presentation = expression.getPresentation(sourceStep);
            return {
                value: value,
                label: presentation.label,
                context: presentation.description,
                isExpression: true,
            };
        }
    }

    public static getAvailableExpressionOptions(p: PlaybookDefinition, s: Step, inRichTextEditor?: boolean): ExpressionOption[] {
        const expressions = PredefinedExpressions
            .all
            .filter(e => e.getSourceStep(p, s, inRichTextEditor));
        return PredefinedExpressions.mapExpressionsToOptions(expressions, p, s, inRichTextEditor);
    }

    public static formatContext(expression: HandlebarExpression, sourceStep: PlacedStep | undefined): string {
        // placedStep could be undefined if there's no trigger yet.
        const output = sourceStep?.type.outputs.find(o => o.name === expression.targetOutput);
        if (output) {
            return output.expressionContext ?? output.description;
        }
        else {
            // Special case for steps that are dispatched for each customer.
            if (sourceStep?.type.additionalDispatchTypes[0] === 'ByCustomer') {
                let context = `dispatched by ${sourceStep.type.presentation.label}`;
                if (expression.targetOutput !== 'customer') {
                    context = `associated with customer ${context}`;
                }
                return context;
            }
        }
        return undefined;
    }

    public static mapExpressionsToOptions(
        expressions: HandlebarExpression[],
        playbook: PlaybookDefinition,
        step: Step,
        inRichTextEditor?: boolean): ExpressionOption[] {
        return expressions
            .map(e => ({
                expression: e,
                sourceStep: e.getSourceStep(playbook, step, inRichTextEditor)
            }))
            .filter(e => !!e.sourceStep)
            .map(e => {
                const presentation = e.expression.getPresentation(e.sourceStep, inRichTextEditor);
                return ({
                    value: e.expression.value,
                    expression: e.expression.expression,
                    label: presentation.label,
                    context: presentation.description,
                    isExpression: true
            })
        });
    }

    public static getAvailableComparisonTypes(expression: string | undefined): ComparisonTypeOption[] {
        const handlebarExpression = PredefinedExpressions.getByValueExpression(expression);
        return handlebarExpression
            ? handlebarExpression.relevantComparisonTypes
            : defaultComparisonTypeOptions;
    }
}

function getSource(sourceStep?: PlacedStep): string | null {
    if (!sourceStep) {
        return null;
    }
    return sourceStep.type.kind === 'Trigger'
        ? 'trigger'
        : 'prior step';
}


const predefinedExpressionsLookup = {
    "channel_name": new HandlebarExpression(
        "trigger.outputs.channel.name",
        "Channel name from trigger",
        {
            targetOutput: 'channel',
            getSourceStep: (p) => p.getTriggerWithOutput("channel", "Channel"),
        }),

    "channel": new HandlebarExpression(
        "outputs.channel.id",
        "Channel from trigger or a prior step",
        {
            targetOutput: 'channel',
            getSourceStep: (p, s, inRichTextEditor) =>
                !inRichTextEditor && (p.getActionStepWithOutput(s.location, "channel", "Channel")
                || p.getTriggerWithOutput("channel", "Channel")),
            comparisonTypeOptions: [
                {value: 'Any', label: 'Is Any Of', title: 'Is true if the channel matches any of the options below.'},
                {value: 'Exists', label: 'Exists', title: 'Is true if the channel exists.'},
                {value: 'NotExists', label: 'Does Not Exist', title: 'Is true if the channel does not exist.'},
            ],
            Input: ({ property, onChange, value }) => {
                if (typeof value === 'string') {
                    value = [];
                }

                return (
                    <ChannelMultiSelect property={property} value={value as Option[]} onChange={onChange} />
                );
            },
            getPresentation: (sourceStep?: PlacedStep) => {
                const label = sourceStep
                    ? `Channel from ${getSource(sourceStep)}`
                    : null;
                return { label };
            },
        }),

    "conversation_state": conversationExpression(
        "trigger.outputs.conversation.state",
        "State of conversation from trigger"),

    "conversation_tags": conversationExpression(
        "trigger.outputs.conversation.tags",
        "Tags of conversation from trigger"),

    "conversation_title": conversationExpression(
        "trigger.outputs.conversation.title",
        "Title of conversation from trigger"),

    "message_url": new HandlebarExpression(
        "trigger.outputs.message.url",
        "Reply to thread from trigger",
        {
            targetOutput: 'message',
            getSourceStep: (p) =>
                p.getTriggerWithOutput("message", "Message"),
            comparisonTypeOptions: [],
            getPresentation: (sourceStep?: PlacedStep, inRichTextEditor?: boolean) => {
                if (inRichTextEditor) {
                    // When we're in the rich text editor, we want to show a different label and description.
                    return sourceStep.type.name === 'abbot.conversation-overdue'
                        ? { label: "Link to message from trigger", description: "in the overdue conversation." }
                        : { label: "Link to message from trigger" };
                }
                else {
                    return { label: undefined, description: undefined }; // Use default behavior.
                }
            },
        }),

    "customer": new HandlebarExpression(
        "outputs.customer.id",
        "Customer from trigger or a prior step",
        {
            targetOutput: 'customer',
            getSourceStep: (p, currentStep, inRichTextEditor) =>
                !inRichTextEditor
                    ? (p.getActionStepWithOutput(currentStep.location, "customer", "Customer")
                    || p.getTriggerWithOutput("customer", "Customer"))
                    : undefined,
            comparisonTypeOptions: [
                {value: 'Any', label: 'Contains Any', title: 'Is true if the customer matches any of the customers below.'},
                {value: 'Exists', label: 'Exists', title: 'Is true if the customer exists.'},
                {value: 'NotExists', label: 'Does Not Exist', title: 'Is true if the customer exist.'},
            ],
            Input: ({ property, onChange, value }) => {
                return (
                    <CustomerMultiSelect property={property} value={value as Option[]} onChange={onChange} />
                );
            },
            getPresentation: (sourceStep?: PlacedStep) => {
                const label = sourceStep
                    ? `Customer from ${getSource(sourceStep)}`
                    : null;
                return { label };
            },
        }),

    "customer_name": new HandlebarExpression(
        "trigger.outputs.customer.name",
        "Customer name from trigger",
        {
            targetOutput: 'customer',
            getSourceStep: (p, currentStep, inRichTextEditor) =>
                inRichTextEditor || currentStep.type.name === 'system.create-customer'
                    ? p.getTriggerWithOutput("customer", "Customer")
                    : undefined,
        }),

    "segment": new HandlebarExpression(
        "trigger.outputs.customer.segments",
        "Customer segment(s) from trigger",
        {
            targetOutput: 'customer',
            getSourceStep: (p) =>
                p.getTriggerWithOutput("customer", "Customer"),
            comparisonTypeOptions: [
                {value: 'All', label: 'Contains All', title: 'Is true if the customer segments contains all of the segments below.'},
                {value: 'Any', label: 'Contains Any', title: 'Is true if any of the customer segments matches any of the segments below.'},
                {value: 'Exists', label: 'Exists', title: 'Is true if the customer has any segments.'},
            ],
            Input: ({ property, onChange, value }) => {
                return (
                    <SegmentsDropDown
                        onChange={onChange}
                        required={!!property.required}
                        value={value as Option[]} />
                );
            }
        }),

    "customer_room_last_activity": new HandlebarExpression(
        "outputs.customer.last_activity_days",
        "Customer days inactive",
        {
            targetOutput: 'customer',
            getSourceStep: (p) => p.getTriggerWithOutput("customer", "Customer"),
            comparisonTypeOptions: [
                {value: 'GreaterThan', label: 'Greater Than'},
                {value: 'LessThan', label: 'Less Than'},
                {value: 'GreaterThanOrEqualTo', label: 'Greater Than or Equal To'},
                {value: 'LessThanOrEqualTo', label: 'Less Than or Equal To'},
                {value: 'Equals', label: 'Equals'}
            ],
            Input: ({ property, onChange, value }) => {
                return (
                    <div className="flex">
                        <TextInput type="number"
                                   placeholder={property.placeholder}
                                   name={property.name}
                                   onChange={onChange}
                                   required={!!property.required}
                                   value={value as string} />
                        <div className="ml-1">Days</div>
                    </div>
                );
            }
        }),

    "arguments": new HandlebarExpression(
        "trigger.outputs.arguments",
        "Arguments from trigger",
        {
            getSourceStep: (p) => p.getTriggerWithOutput("arguments", "String"),
        }),

    "invitation_response": new HandlebarExpression(
        "outputs.invitation_response",
        "Invitation response",
        {
            getSourceStep: (p, s) => p.getOutputAvailableAt(
                s.location,
                "invitation_response",
                "String"),
            comparisonTypeOptions: [
                {value: 'ExactMatch', label: 'Is', title: 'Is true if the invitation response matches the option below.'},
            ],
            Input: ({ property, onChange, value }) => {
                const options = [
                    {value: 'accepted', label: 'Accepted'},
                    {value: 'declined', label: 'Declined'},
                ];

                if (typeof value === 'string') {
                    value = options.filter(o => o.value === value);
                }

                return (
                    <Select name={property.name}
                            options={options}
                            onChange={v => onChange(v.value)}
                            required={!!property.required}
                            value={value as Option[]} />
                );
            }
        }
    ),

    "invitee": new HandlebarExpression(
        "outputs.invitee",
        "Invitee from Customer Info Submission trigger",
        {
            getSourceStep: (p, s, inRichTextEditor) => inRichTextEditor
                && p.getOutputAvailableAt( // This is how we know invitee is probably something they may want to use.
                    s.location,
                    "invitation_response",
                    "String"),
        },
    ),

    "last_poll_response": new HandlebarExpression(
        "outputs.poll_response.value",
        "Last poll response",
        {
            getSourceStep: (p, s) => p.getOutputAvailableAt(
                s.location,
                "poll_response",
                "SelectedOption"),
            comparisonTypeOptions: [
                {value: 'Any', label: 'Is Any Of', title: 'Is true if poll response matches any of the options below.'},
            ],
            Input: ({ property, onChange, value, step }) => {
                const { playbook } = usePlaybook();
                const pollOptions = playbook.findPrecedingPollOptions(step.location);

                if (typeof value === 'string') {
                    value = [];
                }

                return pollOptions
                    ? (
                        <Select name={property.name}
                                options={pollOptions}
                                isMulti={true}
                                onChange={onChange}
                                required={!!property.required}
                                value={value as Option[]} />
                    ): (
                        <div className="text-red-500 font-normal">No poll options found. Add a "Send Slack Poll" before this step.</div>
                    );
            }
        }),

    "last_approver": new HandlebarExpression(
        "outputs.approval_responder.name",
        "Responder to the last approval request",
        {
            getSourceStep: (p, s, inRichTextEditor) =>
                inRichTextEditor && // Don't show this in 'if' steps for now, we don't have a comparison type for it.
                p.getOutputAvailableAt(
                    s.location,
                    "approval_responder",
                    "Member"),
        }),

    "ticket_type": new HandlebarExpression(
        "trigger.outputs.ticket.type",
        "Type of ticket from trigger",
        {
            targetOutput: 'ticket',
            getSourceStep: (p) =>
                p.getTriggerWithOutput("ticket", "Ticket"),
            comparisonTypeOptions: [
                { value: 'Any', label: 'Is One Of', title: 'Is true if the ticket type matches any of the types below.' },
            ],
            Input: ({ property, onChange, value }) => {
                const { stepTypes } = usePlaybook();
                const options = stepTypes.enabledIntegrations
                    .filter(i => i !== 'SlackApp')
                    .map(i => ({ value: i, label: i }));

                if (typeof value === 'string') {
                    value = options.filter(o => o.value === value);
                }

                return (
                    <Select name={property.name}
                        isMulti
                        options={options}
                        onChange={onChange}
                        required={!!property.required}
                        value={value as Option[]} />
                );
            },
        }),

    "ticket_url": ticketExpression(
        "trigger.outputs.ticket.url",
        "Link to ticket from trigger"),

    "ticket_api_url": ticketExpression(
        "trigger.outputs.ticket.api_url",
        "API URL of ticket from trigger"),

    "ticket_id": ticketExpression(
        "trigger.outputs.ticket.ticket_id",
        "Id of ticket from trigger"),

    "ticket_zendesk_status": new HandlebarExpression(
        "trigger.outputs.ticket.zendesk.status",
        "Status of Zendesk ticket from trigger",
        {
            targetOutput: 'ticket',
            getSourceStep: (p) => {
                const t = p.getTriggerWithOutput("ticket", "Ticket");
                if (t?.type.requiredIntegrations?.includes('Zendesk')) {
                    return t;
                }
            },
            comparisonTypeOptions: [
                { value: 'Any', label: 'Is One Of', title: 'Is true if the ticket matches any of the statuses below.' },
            ],
            Input: ({ onChange, value }) => {
                const options = ['New', 'Open', 'Pending', 'Solved', 'Closed']
                    .map(value => ({ value, label: value }));

                if (typeof value === 'string') {
                    value = options.filter(o => o.value === value);
                }

                return (
                    <Select
                        isMulti
                        onChange={onChange}
                        options={options}
                        placeholder="Select one or more statuses"
                        required
                        value={value as Option[]}
                    />
                );
            },
        }),

    "ticket_github_owner": ticketExpression(
        "trigger.outputs.ticket.github.owner",
        "Owner of GitHub Issue from trigger"),

    "ticket_github_repo": ticketExpression(
        "trigger.outputs.ticket.github.repo",
        "Repository of GitHub Issue from trigger"),

    "ticket_hubspot_thread_id": ticketExpression(
        "trigger.outputs.ticket.hubspot.thread_id",
        "Thread ID of HubSpot ticket from trigger"),
};

function conversationExpression(expression: string, name: string) {
    return new HandlebarExpression(
        expression,
        name,
        {
            targetOutput: 'conversation',
            getSourceStep: (p, _s, inRichTextEditor) =>
                inRichTextEditor &&
                p.getTriggerWithOutput("conversation", "Conversation"),
            comparisonTypeOptions: [],
        });
}
function ticketExpression(expression: string, name: string) {
    return new HandlebarExpression(
        expression,
        name,
        {
            targetOutput: 'ticket',
            getSourceStep: (p, _s, inRichTextEditor) =>
                inRichTextEditor &&
                p.getTriggerWithOutput("ticket", "Ticket"),
            comparisonTypeOptions: [],
        });
}