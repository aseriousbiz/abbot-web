import usePlaybookActivity, { PlaybookAction } from "../hooks/usePlaybookActivity";
import usePlaybook from "../hooks/usePlaybooks";
import Panel, { PanelProps } from "./panel";
import Debug from "./debug";
import { isStaffMode } from "../../ts/env";
import { StepTypeBlock } from "./stepBlock";
import { Step, StepKind } from "../models/step";
import { StepType } from "../models/stepTypeCatalog";
import { useCallback, useMemo, useState } from "react";

export interface StepKindPanelProps extends PanelProps {
    kind: StepKind,
}

function scoreStepTypeMatch(stepType: StepType, filter: string): number {
    filter = filter.trim().toLocaleLowerCase();

    if (stepType.presentation.label) {
        const stepTypeLabel = stepType.presentation.label.toLocaleLowerCase();

        // Prioritize an exact match.
        if (stepTypeLabel === filter) {
            return 3;
        }

        if (stepTypeLabel.includes(filter)) {
            return 2;
        }
    }

    if (stepType.name && stepType.name.toLocaleLowerCase().includes(filter)) {
        return 1;
    }

    if (stepType.presentation.description && stepType.presentation.description.toLocaleLowerCase().includes(filter)) {
        return 1;
    }

    return 0;
}

export default function StepKindPanel({ kind, title, className }: StepKindPanelProps) {
    const {currentAction, pushAction} = usePlaybookActivity();
    const {playbook, stepTypes} = usePlaybook();
    const [filter, setFilter] = useState<string>("");

    const canBePlaced = useCallback((stepType: StepType) => {
        if (!currentAction) {
            return false;
        }

        if (stepType.deprecated) {
            return false;
        }

        if (stepType.kind !== kind) {
            return false;
        }

        // Staff only steps are only for staff... ONLY!
        if (stepType.staffOnly && !isStaffMode) {
            return false;
        }

        // If the step type has feature flags, we need to check if the playbook has those flags enabled
        if (!stepTypes.hasFeatureFlags(stepType)) {
            return false;
        }

        // If the step type has integrations, we need to check if the organization has those integrations
        if (!stepTypes.hasIntegrations(stepType)) {
            return false;
        }

        // We don't allow any steps with branching unless you're in the start sequence
        if (currentAction.location.sequence !== playbook.startSequence && stepType.branches.length > 0) {
            return false;
        }

        if (stepType.name === 'system.complete-playbook' && currentAction.location.sequence === playbook.startSequence) {
            // If we're not in a branch, we can't place an "Complete Playbook" step.
            return false;
        }

        // I can't think of a reason why not, so knock yourself out bud.
        return true;
    }, [playbook, stepTypes, currentAction]);

    const filteredStepTypes = useMemo(
        () => {
            const filtered = stepTypes.types.filter(canBePlaced);
            return filter.length > 0
                ? filtered.map(step => [scoreStepTypeMatch(step, filter), step] as [number, StepType])
                    .filter(pair => pair[0] > 0)
                    .sort((a, b) => b[0] - a[0])
                    .map(pair => pair[1])
                : filtered;
        },
        [canBePlaced, stepTypes, filter]);

    function handleClick(stepType: StepType) {
        const step = Step.createNew(stepType, currentAction.location);

        // Mark the candidate step as active
        pushAction(PlaybookAction.edit(step));

        // Clear the filter for next time.
        setFilter("");

        // DO NOT save! The playbook is saved when the step properties are saved.
    }

    if (!currentAction?.isInserting(kind)) {
        return null;
    }

    const stepTypeBlocks = filteredStepTypes
        .map(stepType => (
            <StepTypeBlock
                key={stepType.name}
                stepType={stepType}
                onClick={() => handleClick(stepType)} />
        )
    );

    return (
        <Panel id={currentAction.kind} title={title} className={className || ''}>
            <div className="flex flex-col gap-2">
                <input
                    autoFocus={true}
                    className="form-input w-full"
                    placeholder="Filter"
                    value={filter}
                    onChange={(e) => setFilter(e.currentTarget.value)} />
                {!stepTypes.enabledIntegrations.includes("SlackApp") && <div className="flex gap-2 text-sm items-center ml-2 text-yellow-600">
                    <i className="fa fa-exclamation-triangle"></i>
                    Additional steps are available once you've connected Abbot to Slack
                </div>}
                <div className="flex flex-col gap-y-1">
                    {stepTypeBlocks}
                </div>
                <Debug>
                    <code>{currentAction?.location.toString()}</code>
                </Debug>
            </div>
        </Panel>
    );
}
