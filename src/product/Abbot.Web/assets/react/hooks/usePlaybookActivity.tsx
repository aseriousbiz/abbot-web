import * as React from "react";
import {createContext, useContext, useEffect, useMemo, useState} from "react";
import useActivePanel from "./useActivePanel";
import {PanelKind} from "../components/panel";
import { Step, StepKind, StepLocation } from "../models/step";
import logger from "../../ts/log";

const log = logger("usePlaybookActivity");

/** 
 * Represents a Action that can be active in the Playbook.
 * 
 * This could be a request to insert a new step, or a request to edit an existing step.
 */
export class PlaybookAction {
    private constructor(
        public kind: StepKind,
        public location: StepLocation,
        public step?: Step) {
    }

    static insert(kind: StepKind, location: StepLocation): PlaybookAction {
        return new PlaybookAction(kind, location);
    }

    static edit(step: Step): PlaybookAction {
        return new PlaybookAction(step.type.kind, step.location, step);
    }

    /** Returns a boolean indicating if this is a request to insert a step of the specified kind, or a request of any kind if not specified.
     * 
     * A "Proposal" means there is no 'Step' object associated with this PlaybookAction, it's a request to add a new step.
     */
    isInserting(kind?: StepKind): this is PlaybookAction & { step: undefined } {
        return this.step === undefined && (kind === undefined || this.kind === kind);
    }

    /** Returns a boolean indicating if this is a request to edit a step with the specified ID, or any ID if not specified. */
    isEditing(id?: string): this is PlaybookAction & {step: Step} {
        return this.step !== undefined && (id === undefined || this.step.id === id);
    }

    toString(): string {
        if (this.isInserting()) {
            return `InsertStep("${this.kind}", "${this.location.toString()}")`;
        } else if (this.isEditing()) {
            return `EditStep("${this.kind}", "${this.location.toString()}", "${this.step.id}")`
        }
    }
}

export interface PlaybookActivityContextState {
    currentAction: PlaybookAction,
    pushAction: (action: PlaybookAction) => void,
    popAction: () => void,
    clearActionStack: () => void,
}

const PlaybookActivityContext = createContext(null as PlaybookActivityContextState | null);

export default function usePlaybookActivity() {
    const context = useContext(PlaybookActivityContext);
    if (!context) throw new Error("PlaybookActivityContext not found!");
    return context;
}

export function PlaybookActivityContextProvider(props: {children: React.ReactNode}) {
    // Actions are tracked in a Stack.
    // To manage the stack, we use an array where [0] is the TOP of the stack.
    // Doing it this way, instead of using 'push' and 'pop' which treat the end of the array as the TOP, makes immutible push/pop operations cleaner.
    const [actionStack, setActionStack] = useState<PlaybookAction[]>([]);
    const {activePanel, setActivePanel} = useActivePanel();

    const currentAction = useMemo(() => actionStack.length > 0 ? actionStack[0] : null, [actionStack]);

    function pushAction(active: PlaybookAction | null) {
        const newStack = [active, ...actionStack];
        log.verbose(`Pushing Action: [${actionStack}] --> [${newStack}]`);
        setActionStack(newStack);
    }

    function popAction() {
        const newStack = actionStack.slice(1)
        log.verbose(`Popping Action: [${actionStack}] --> [${newStack}]`);
        setActionStack(newStack);
    }

    function clearActionStack() {
        log.verbose("Clearing action stack");
        setActionStack([]);
    }

    const value = useMemo(() => ({
        currentAction,
        pushAction,
        popAction,
        clearActionStack,
    }), [currentAction, activePanel]);

    useEffect(() => {
        if (currentAction?.isEditing()) {
            setActivePanel('Properties');
        }
        else if (currentAction?.isInserting()) {
            setActivePanel(currentAction.kind as PanelKind);
        }
        else {
            setActivePanel('None');
        }
    }, [currentAction]);

    // Hook the keydown/keyup events to close the panel when the escape key is pressed.
    useEffect(() => {
        function handleKeyUp(event: KeyboardEvent) {
            if (event.key === 'Escape') {
                // Esc means exit all panels. So clear the stack.
                clearActionStack();
            }
        }

        window.addEventListener('keyup', handleKeyUp);
        return () => window.removeEventListener('keyup', handleKeyUp);
    }, [currentAction]);

    return (
        <PlaybookActivityContext.Provider value={value}>
            { props.children }
        </PlaybookActivityContext.Provider>
    );
}