import usePlaybookActivity from "../hooks/usePlaybookActivity";
import * as React from "react";
import {useCallback, useEffect, useRef, useState} from "react";
import Panel, {PanelProps} from "./panel";
import StepKindBlock from "./stepKindBlock";
import Debug from "./debug";
import usePlaybook from "../hooks/usePlaybooks";
import {isStaffMode} from "../../ts/env";
import StepPropertyEditor from "./propertyEditors/stepPropertyEditor";
import useReadonly from "../hooks/useReadonly";
import logger from "../../ts/log";
import {StepProperty} from "../models/step";
import CustomerInfoDetails from "./customerInfoDetails";
import {InviteeDetails} from "./inviteeDetails";

const log = logger("PropertiesPanel");

export default function PropertiesPanel({title, className}: PanelProps) {
    const [inputs, setInputs] = useState<Record<string, unknown>>({});
    const {currentAction, popAction, clearActionStack} = usePlaybookActivity();
    const {playbook, setPlaybook, savePlaybook} = usePlaybook();
    const {readonly} = useReadonly();

    // Reset initial JSON for each new activeStep
    useEffect(() => {
        if (currentAction?.isEditing()) {
            setInputs(
                Object.assign(
                    getDefaults(currentAction.step.type.inputs ?? []),
                    currentAction.step.inputs));
        }
    }, [currentAction])

    const handleChange = useCallback((property: StepProperty, value: unknown) => {
        log.log('Property changed', { property, value });
        setInputs(json => ({
            ...json,
            [property.name]: value,
        }));
    }, []);

    const handleSubmit = useCallback((e: React.FormEvent<HTMLFormElement>) => {
        if (!currentAction?.isEditing()) {
            return null;
        }

        // Prevent the browser from reloading the page
        e.preventDefault();

        // First, add the step if it's new
        let sourceStep = currentAction.step;
        let newPlaybook = playbook;
        if (!currentAction.step.id) {
            // We have to insert the step into the playbook
            const id = newPlaybook.generateStepId(currentAction.step.type.getStepName());
            sourceStep = sourceStep.withId(id);
            newPlaybook = newPlaybook.addStep(
                sourceStep,
                currentAction.location);
            sourceStep = newPlaybook.getStep(sourceStep.location.sequence, sourceStep.id);
        }

        // Update the inputs from the state
        let newStep = sourceStep.withInputs(inputs);

        // Check if we need to add any branch bindings
        const branches = {...newStep.branches};
        let hasNewBranches = false;
        step.type.branches.forEach(branch => {
            if(!branches[branch.name]) {
                hasNewBranches = true;
                const sequenceId = newPlaybook.generateSequenceId(`${newStep.id}:${branch.name}`);
                newPlaybook = newPlaybook.addSequence(sequenceId);
                branches[branch.name] = sequenceId;
            }
        });
        if (hasNewBranches) {
            newStep = newStep.withBranches(branches);
        }

        // Replace the step in the playbook
        newPlaybook = newPlaybook.replaceStep(sourceStep, newStep);
        setPlaybook(newPlaybook);
        savePlaybook();

        // We saved, so wipe the action stack.
        clearActionStack();
    }, [playbook, currentAction, inputs]);

    function handleDeleteClick() {
        if (!currentAction?.isEditing()) {
            return null;
        }

        const newPlaybook = playbook.removeStep(currentAction.step);
        setPlaybook(newPlaybook);
        savePlaybook();

        // Deleting the step => wipe the action stack.
        clearActionStack();
    }

    const saveButtonRef = useRef<HTMLButtonElement>();

    // Hook Ctrl/Cmd-S to save
    useEffect(() => {
        function handleKeyDown(event: KeyboardEvent) {
            if (event.ctrlKey || event.metaKey) {
                const saveButton = saveButtonRef.current;
                switch (event.key) {
                    case 's':
                        event.preventDefault();
                        event.stopPropagation();

                        log.log('Saving via keyboard shortcut');
                        saveButton?.form.requestSubmit(saveButton);
                }
            }
        }

        window.addEventListener('keydown', handleKeyDown);
        return () => window.removeEventListener('keydown', handleKeyDown);
    }, []);

    if (!currentAction?.isEditing()) {
        return null;
    }
    const step = currentAction.step;

    const propertyEditors = step.type.inputs.map(property => (
        <StepPropertyEditor
            key={`${step.id || 'new'}-${property.name}`}
            step={step}
            property={property}
            value={inputs[property.name]}
            inputs={inputs}
            onChange={v => handleChange(property, v)} />
    ));

    const isNewStep = !step.id;

    const customerWebhookDetails = step.type.name === 'http.webhook.customer'
        ? (
            <CustomerInfoDetails playbook={playbook} isStaffMode={isStaffMode} step={step} />
        )
        : null;

    const createCustomerInstructions = step.type.name === 'system.create-customer'
        ? (
            <p className="text-slate-500 text-sm select-none">
                Uses information from the <code>Customer Info Submitted</code> trigger.
            </p>
        )
        : null;

    const inviteeInstructions = step.type.name === 'slack.invite-to-shared-channel'
        ? (
            <InviteeDetails />
        )
        : null;

    const isUnknown = step.type.category === 'unknown';

    return (
        <Panel id={"Properties"} title={title} className={className || ''}>
            <div className="flex flex-col gap-y-1">
                <form className="p-1" onSubmit={!readonly && handleSubmit} data-autobind="false">
                    <StepKindBlock stepType={step.type} />

                    <div className="my-4">
                        {customerWebhookDetails}
                        {createCustomerInstructions}
                        {inviteeInstructions}
                        {!isUnknown && propertyEditors}
                    </div>

                    <div className="mt-4 flex gap-x-2">
                        {!isNewStep && (
                            <div>
                                <button onClick={!readonly && handleDeleteClick}
                                        className="btn btn-danger shadow-sm"
                                        type="button">
                                    Delete
                                </button>
                            </div>
                        )}

                        <div className="ml-auto">
                            <button type="button"
                                    onClick={popAction /* Cancelling just pops the current action, doesn't wipe the stack */}
                                    className="btn">
                                Cancel
                            </button>
                            {!isUnknown && (
                                <button type="submit"
                                        ref={saveButtonRef}
                                        className="btn btn-primary ml-1">
                                    Save
                                </button>
                            )}
                        </div>
                    </div>
                </form>
            </div>
            <Debug>
                <code className="text-xs my-4">{currentAction.step.toString()}</code>
                <pre className="text-xs overflow-auto">{JSON.stringify(inputs, null, 2)}</pre>
            </Debug>
        </Panel>
    );
}

function getDefaults(inputs: StepProperty[]): Record<string, unknown> {
    return inputs.reduce((defaults, p) => {
        defaults[p.name] = p.default;
        return defaults;
    }, {});
}
