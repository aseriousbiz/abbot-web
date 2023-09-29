import usePlaybook from "../hooks/usePlaybooks";
import {
    closestCenter,
    DndContext,
    DragEndEvent,
    KeyboardSensor,
    PointerSensor,
    UniqueIdentifier,
    useSensor,
    useSensors
} from "@dnd-kit/core";
import {SortableContext, sortableKeyboardCoordinates, verticalListSortingStrategy} from "@dnd-kit/sortable";
import * as React from "react";
import {StepBlock} from "./stepBlock";
import {restrictToVerticalAxis} from "@dnd-kit/modifiers";
import usePlaybookActivity, { PlaybookAction } from "../hooks/usePlaybookActivity";
import DragHandle from "./dragHandle";
import AddActionButton from "./addActionButton";
import SortableItem from "./sortableItem";
import useReadonly from "../hooks/useReadonly";
import { SavedStep } from "../models/step";
import { isStaffMode } from "../../ts/env";

function stepsToLookup(array: SavedStep[]): { [id: UniqueIdentifier]: SavedStep } {
    return array.reduce((lookup, step: SavedStep) => {
        lookup[step.uniqueId] = step;
        return lookup;
    }, {});
}

type ActionBlocksProps = {
    sequence: string;
    compact?: boolean;
}

export function ActionBlocks({ sequence, compact }: ActionBlocksProps) {
    const {playbook, setPlaybook, savePlaybook} = usePlaybook();
    const {pushAction} = usePlaybookActivity();
    const {readonly} = useReadonly();

    const sensors = useSensors(
        useSensor(PointerSensor),
        useSensor(KeyboardSensor, {
            coordinateGetter: sortableKeyboardCoordinates,
        })
    );

    const steps = playbook
        ? playbook.sequences[sequence].steps
        : [];

    const actionsLookup = stepsToLookup(steps);

    function handleActionClicked(e: React.MouseEvent<HTMLElement>, step: SavedStep) {
        e.stopPropagation();
        if (readonly || (step.type.staffOnly && !isStaffMode)) {
            return;
        }
        pushAction(PlaybookAction.edit(step));
    }

    function handleDragEnd(event: DragEndEvent) {
        const {active, over} = event;

        if (active.id !== over.id) {
            const from = actionsLookup[active.id];
            const to = actionsLookup[over.id];

            const newPlaybook = playbook.moveStep(from, to.location);
            setPlaybook(newPlaybook);
            savePlaybook();
        }
    }

    const hoverClasses = readonly
        ? null
        : 'border border-transparent border-solid hover:border-gray-200 hover:bg-gray-100';

    const actions = steps
        .map(placed => (
            <React.Fragment key={placed.uniqueId}>
                {!compact && <AddActionButton location={placed.location} />}
                <SortableItem id={placed.uniqueId}>
                    <StepBlock
                        step={placed}
                        onClick={readonly ? undefined : ((e) => handleActionClicked(e, placed))}>
                        {!readonly &&
                            <DragHandle className={`ml-auto py-2 -my-2 px-4 mr-0 rounded rounded-lg ${hoverClasses}`}>
                                <i className="fa-solid fa-grip-dots-vertical"></i>
                            </DragHandle>}
                    </StepBlock>
                </SortableItem>
            </React.Fragment>
        ));

    return (
        <DndContext collisionDetection={closestCenter}
                    sensors={sensors}
                    modifiers={[restrictToVerticalAxis]}
                    onDragEnd={!readonly && handleDragEnd}>
            <SortableContext items={steps.map(s => s.uniqueId)}
                             strategy={verticalListSortingStrategy}>
                {actions}
            </SortableContext>
        </DndContext>
    );
}

