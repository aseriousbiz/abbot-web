import React, {PropsWithChildren} from "react";
import StepKindPresentation from "../models/stepKindPresentation";
import {IntegrationType, PlacedStep, StepBranch, StepLocation, StepProperty} from "../models/step";
import Debug from "./debug";
import usePlaybookActivity from "../hooks/usePlaybookActivity";
import {isStaffMode} from "../../ts/env";
import {PropertyViewer} from "./propertyViewer";
import useReadonly from "../hooks/useReadonly";
import {StepType} from "../models/stepTypeCatalog";
import {ActionBlocks} from "./actionBlocks";
import AddActionButton from "./addActionButton";
import FeatureSparkle from "./featureSparkle";
import usePlaybook from "../hooks/usePlaybooks";
import {ComparisonType} from "../models/comparisonType";
import {Tooltip} from "react-tooltip";

export interface StepTypeProps {
    stepType: StepType,
    className?: string,
    onClick?: (e: React.MouseEvent<HTMLElement>) => void,
}

export interface PlacedStepBlockProps {
    step: PlacedStep,
    className?: string,
    onClick?: (e: React.MouseEvent<HTMLElement>) => void,
}

function describeIntegration(integration: IntegrationType): string {
    if (integration === 'SlackApp') {
        return 'Slack';
    }
    return integration;
}

function formatProperty(
    value: unknown,
    inputs: Record<string, unknown>,
    property?: StepProperty,
    step?: PlacedStep): React.ReactNode {

    if (!property || !property.type) {
        // TODO: We could continue rendering here in prod, but in dev we should throw an error.
        throw new Error("Could not find property");
    }

    if (property.hidden && !isStaffMode) {
        return null;
    }

    const comparison = inputs['comparison'] as ComparisonType;
    if (property.name === 'right' && (comparison === 'Exists' || comparison === 'NotExists')) {
        return null;
    }

    const propertyViewer = value !== undefined && value !== null
        ? (
            <PropertyViewer property={property}
                            value={value}
                            inputs={inputs}
                            step={step} />
        )
        : (
            <em className="text-slate-500">Not set</em>
        );

    return <dl key={property.name}
        className="property-preview-item flex-1 flex-inline flex-wrap font-medium text-xs my-1 w-full">
        <dt className="property-name text-slate-500 text-left">
            {property.title}
            {property.hidden && (
                <span className="ml-1" data-tooltip="Visible to Abbot Staff only.">
                    <i className="fa-duotone fa-shield-check"></i>
                </span>
            )}
        </dt>
        <dd className="property-value text-left">{propertyViewer}</dd>
    </dl>
}

function StepTypeContainer({ className, stepType, onClick, children } : PropsWithChildren<StepTypeProps>) {
    const { playbook, stepTypes } = usePlaybook();
    const {readonly} = useReadonly();
    const colors = StepKindPresentation.getPresentation(stepType.kind).colors;
    const isReadonly = readonly || (stepType.staffOnly && !isStaffMode);
    const hoverClasses = isReadonly ? 'hover:cursor-default' : 'hover:cursor-pointer hover:opacity-100';

    function handleStepClick(e: React.MouseEvent<HTMLElement>) {
        e.stopPropagation();
        e.preventDefault();
        if (onClick) {
            onClick(e);
        }
    }

    function getBgAndBorderClasses() {
        const { requiredTriggersExist } = playbook.hasRequiredTriggers(stepType, stepTypes);
        if (!requiredTriggersExist) {
            return "bg-slate-100";
        }
        return stepType.staffOnly ? "border-dotted bg-slate-100" : "bg-white";
    }

    return <div onClick={handleStepClick}
            className={`${className || ''} w-full flex-none select-none pl-2 pr-1 py-2 flex gap-x-2 items-center justify-items-center rounded-lg border shadow-sm ${colors.border} ${getBgAndBorderClasses()} ${hoverClasses}`}>
        {children}
    </div>
}

function StepTypeBlockInner({ stepType, children, step }: PropsWithChildren<{stepType: StepType, step?: PlacedStep}>) {
    const { stepTypes, playbook } = usePlaybook();
    const {icon, label, description} = stepType.presentation;
    const colors = StepKindPresentation.getPresentation(stepType.kind).colors;

    const { requiredTriggersExist, requiredTriggers } = playbook.hasRequiredTriggers(stepType, stepTypes);

    // Check if the step is missing any integrations
    const missingIntegrations = stepType.requiredIntegrations
        .filter(f => !stepTypes.enabledIntegrations.includes(f));
    const missingIntegrationsMessage = missingIntegrations.length === 0
        ? null
        : `This step requires you to enable the ${describeIntegration(missingIntegrations[0])} integration.`;
    const missingTriggersMessage = requiredTriggersExist
        ? null
        : `This step requires one of the following triggers: ${requiredTriggers.map(t => t.presentation.label).join(', ')}`;

    // This component (StepTypeBlockInner) shows up in 2 places, the main playbook surface as a placed step, and in
    // the side Step Kind panel when choosing a Trigger or Action to add.
    // We need a unique tooltip Id for each of those cases, otherwise the same tooltip in both locations tries to
    // render at the same time and we get double.
    // Hence the tooltipId is based on whether we have a step or not.
    // The nice thing is multiple components can re-use the same <Tooltip /> component as long as they reference it
    // by the same id.
    const tooltipId = `tooltip-step-block-${step ? 'step' : 'kind'}`;

    return <div className="flex gap-x-2">
        <Tooltip id={tooltipId} />
        <div className={`rounded ${colors.iconBgColor} p-2 flex items-center w-8 h-8`}>
            <i className={`far ${icon} ${colors.iconTextColor}`}></i>
        </div>

        <div className="pr-3">
            <p className="flex gap-x-1 items-baseline font-semibold text-sm text-left">
                {label}
                {
                    stepType.staffOnly && (
                        <>
                            <span className="ml-1"
                                  data-tooltip-id={tooltipId}
                                  data-tooltip-content="Abbot staff created this step for you, contact support to update it.">
                                <i className="fa-duotone fa-shield-check"></i>
                            </span>
                        </>

                    )
                }
                {stepType.requiredFeatureFlags?.length > 0 && (<FeatureSparkle flags={stepType.requiredFeatureFlags} />)}
                {missingIntegrationsMessage && (
                    <>
                        <span className="ml-1 text-yellow-600 has-tooltip-arrow"
                              data-tooltip-id={tooltipId}
                              data-tooltip-content={missingIntegrationsMessage}>
                            <i className="fa fa-exclamation-triangle"></i>
                        </span>
                    </>
                )}
                {missingTriggersMessage && (
                    <>
                        <span className="ml-1 text-yellow-600 has-tooltip-arrow"
                              data-tooltip-id={tooltipId}
                              data-tooltip-content={missingTriggersMessage}>
                            <i className="fa fa-exclamation-triangle"></i>
                        </span>
                    </>
                )}
            </p>
            <Debug>
                <code className="text-xs">{stepType.name}</code>
            </Debug>
            
            <p className="text-sm text-slate-500">{description}</p>

            {children}
        </div>
    </div>
}

export function StepTypeBlock({ stepType, onClick, className }: StepTypeProps) {
    return (
        <StepTypeContainer stepType={stepType} className={className} onClick={onClick}>
            <StepTypeBlockInner stepType={stepType} />
        </StepTypeContainer>
    );
}

type BranchLaneProps = {
    branch: StepBranch,
    sequenceId: string,
}

export function BranchLane({ branch, sequenceId } : BranchLaneProps) {
    const { playbook } = usePlaybook();

    // We hook onClick and set the cursor to a regular pointer because we're starting a new "context" for the click.
    // It doesn't make sense to click on a branch lane, which could be the result of misclicking between two steps in the lane.
    // If we don't stop propagation, the click will bubble up to the block containing this lane, which would be jarring.
    // The blocks within this lane will hook onClick and set the cursor to a pointer, so this should be clear to the user.
    const hasEndPlaybookStep = playbook.sequenceHasStep(sequenceId, 'system.complete-playbook');

    return <div
        className="flex-1 cursor-default my-2 flex flex-col gap-2 px-4"
        onClick={(e) => e.stopPropagation()}>
        <header className="text-center">
            <h2 className="bg-gray-200 font-medium inline-block px-2 rounded-full text-gray-700 text-sm">
                {branch.title}…
            </h2>
            <Debug>
                <code>{branch.name} ➡️ {sequenceId}</code>
            </Debug>
        </header>

        <ActionBlocks sequence={sequenceId} compact={true} />
        {!hasEndPlaybookStep && (
            <>
            <AddActionButton location={new StepLocation(sequenceId)} compact={true} />
            <div className="text-slate-500 font-medium text-xs text-center">…then run the rest of the Playbook</div>
            </>
        )}
    </div>
}

export function StepBlock({ step, onClick, children, className } : PropsWithChildren<PlacedStepBlockProps>) {
    const {currentAction} = usePlaybookActivity();
    const stepType = step.type;
    const isUnknown = stepType.category === 'unknown';
    const inputs = step.inputs;

    const stepInputs = (!stepType.staffOnly || isStaffMode) && inputs
        ? stepType.inputs.map(i => formatProperty(inputs[i.name], inputs, i, step))
        : [];

    const activeCssClasses = currentAction?.isEditing(step.id)
        ? 'relative z-20'
        : '';

    const branchLanes = step.type.branches.map(b => {
        const sequenceId = step.branches[b.name];
        if (!sequenceId) {
            // This can happen if the step was added by older code that didn't set the branch.
            // For now, just don't put the branch lane down.
            // The loader should resolve this if the user hard-refreshes.
            return undefined;
        }
        return <BranchLane key={b.name} branch={b} sequenceId={sequenceId} />
    });


    return (
        <div>
            <StepTypeContainer
                stepType={step.type}
                onClick={onClick}
                className={`${className || ''} ${activeCssClasses}`}>
                <StepTypeBlockInner stepType={stepType} step={step}>
                    <div className="properties-previews flex flex-col items-start gap-y-1">
                        {!isUnknown && stepInputs}

                        <Debug>
                            <div className="flex">
                                <code className="text-xs">{step.toString()}</code>
                            </div>
                        </Debug>
                    </div>
                </StepTypeBlockInner>
                {children}
            </StepTypeContainer>
            
            {branchLanes && <div className="lg:flex gap-2">
                {branchLanes}
            </div>}
        </div>
    );
}