import usePlaybook from "../hooks/usePlaybooks";
import usePlaybookActivity, { PlaybookAction } from "../hooks/usePlaybookActivity";
import useReadonly from "../hooks/useReadonly";
import DispatchSettings from "../components/dispatchSettings";
import { StepBlock } from "./stepBlock";
import { SavedStep, StepLocation } from "../models/step";
import { isStaffMode } from "../../ts/env";

export default function TriggerBlocks() {
    const {playbook} = usePlaybook();
    const {currentAction, pushAction} = usePlaybookActivity();
    const {readonly} = useReadonly();

    function handleNewTriggerClick() {
        if (readonly) {
            return;
        }
        pushAction(PlaybookAction.insert(
            "Trigger",
            new StepLocation(playbook.triggers.id)));
    }

    function handleTriggerClicked(e: React.MouseEvent<HTMLElement>, step: SavedStep) {
        e.stopPropagation();
        if (readonly || (step.type.staffOnly && !isStaffMode)) {
            return;
        }
        pushAction(PlaybookAction.edit(step));
    }

    const triggers = playbook
        ? playbook.triggers.steps
            .map(trigger => (
                <StepBlock key={trigger.id}
                           step={trigger}
                           className="mt-1"
                           onClick={!readonly && ((e) => handleTriggerClicked(e, trigger))} />
            ))
        : [];

    const addTriggerCssClass = currentAction?.isInserting("Trigger")
        ? 'relative z-20 bg-white'
        : '';

    const hoverClasses = readonly
        ? null
        : "hover:text-indigo-600 hover:border-indigo-600";

    return (
        <>
            <div id="triggers-section" className="flex flex-col gap-y-1 border border-slate-200 bg-white p-2 rounded-xl">
                <p className="text-xs text-slate-500 font-medium">Trigger</p>
                <div id="triggers">
                    {triggers}
                </div>
                {!readonly && triggers.length === 0 && (
                    <button className={`${addTriggerCssClass} rounded-lg border border-dashed border-slate-300 px-8 py-2 flex flex-col items-center text-slate-500 ${hoverClasses}`}
                            onClick={!readonly && handleNewTriggerClick}>
                        <i className="fa-regular fa-circle-plus"></i>
                        <div className="font-medium text-xs">Add a trigger</div>
                    </button>
                )}
                <DispatchSettings />
            </div>
            <i className="far fa-arrow-down-long text-slate-400 triggers-section-arrow hidden"></i>
        </>
    );
}
