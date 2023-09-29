import React from "react";
import usePlaybookActivity, { PlaybookAction } from "../hooks/usePlaybookActivity";
import useReadonly from "../hooks/useReadonly";
import { StepLocation } from "../models/step";

type AddActionButtonProps = {
    location: StepLocation,
    showLabel?: boolean,
    compact?: boolean
}

export default function AddActionButton({location, showLabel, compact}: AddActionButtonProps) {
    const {currentAction, pushAction} = usePlaybookActivity();
    const {readonly} = useReadonly();

    function handleNewStepClick(e: React.MouseEvent<HTMLElement>) {
        // We could be nested, so stop propagation to prevent the click from hitting the parent.
        e.stopPropagation();
        pushAction(PlaybookAction.insert(
            "Action",
            location));
    }

    // The button is active if both of the following are true:
    // * Active request is for an "Action" step.
    // * Active request is for this location in the playbook.
    const addActionCssClass = currentAction?.isInserting('Action') && StepLocation.equal(currentAction?.location, location)
        ? 'z-20 bg-white'
        : readonly
            ? 'cursor-default'
            : null;

    return (
        <div className="gap-y-0.5 w-full flex mx-auto px-6 rounded-lg flex-col items-center">
            {readonly
                ? null
                : (
                    <>
                        {!compact && <i className="fa-regular fa-pipe text-slate-400"></i>}
                        <button type="button"
                                onClick={!readonly && handleNewStepClick}
                                className={`flex flex-col items-center text-slate-500 relative w-full ${compact ? 'mt-0.5' : 'py-2 -my-2'} rounded-lg border border-transparent border-dashed transition-colors hover:text-indigo-600 hover:border-indigo-600 ${addActionCssClass}`}
                                aria-label="Add a step">
                            <i className="fa-regular fa-circle-plus"></i>
                            { showLabel
                                ? (<span className="text-sm">Add an action</span>)
                                : null}
                        </button>
                    </>
                )}
            {location.index !== undefined
                ? (<i className="far fa-arrow-down-long text-slate-400 triggers-section-arrow"></i>)
                : null}
        </div>
    );
}