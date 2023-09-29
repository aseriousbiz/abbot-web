import {createContext, useContext, useEffect, useMemo, useState} from "react";
import * as Api from "../../ts/api/internal";
import PlaybookDefinitionLoader from "../models/playbookDefinitionLoader";
import {useFetch} from "usehooks-ts";
import Loading from "../components/loading";
import { PlaybookDefinition } from "../models/playbookDefinition";
import * as React from "react";
import { StepTypeCatalog } from "../models/stepTypeCatalog";
import logger from "../../ts/log";

const log = logger("usePlaybook");

export interface PlaybookContextState {
    readonly playbook: PlaybookDefinition,
    readonly dirty: boolean,
    readonly stepTypes: StepTypeCatalog,

    /**
     * Updated the playbook definition in the context, but does not save it.
     * @param playbook The new playbook definition
     */
    setPlaybook(playbook: PlaybookDefinition): void;

    /**
     * Requests that the playbook definition be saved on the next render cycle.
     */
    savePlaybook(): void;
}

export const PlaybookContext = createContext(null as PlaybookContextState | null);

export default function usePlaybook() {
    const context = useContext(PlaybookContext);
    if (!context) throw new Error("PlaybookContext not found!");
    return context;
}

export function PlaybookContextProvider(props: {playbookDefinitionLoader: PlaybookDefinitionLoader, children: React.ReactNode}) {
    const [playbook, setPlaybook] = useState(null as PlaybookDefinition);
    const [dirty, setDirty] = useState(false);
    const [saveRequested, setSaveRequested] = useState(false);
    const { error, data: stepTypeList } = useFetch<Api.StepTypeList>('/api/internal/stepTypes');
    const [stepTypes, setStepTypes] = useState<StepTypeCatalog | null>(null);

    if (error) {
        // We can't continue without step types, so throw an error and the entire app will "crash" gracefully.
        throw new Error("Error loading step types: " + error.message);
    }

    useEffect(() => {
        if (stepTypeList) {
            // Create the step type catalog
            const stepTypes = StepTypeCatalog.fromApi(stepTypeList);

            // When the step types are loaded, we can resolve a playbook definition.
            const initialPlaybook = PlaybookDefinition.resolve(
                props.playbookDefinitionLoader.load(),
                stepTypes,
                props.playbookDefinitionLoader.webhookTriggerUrl);
            setPlaybook(initialPlaybook);
            setStepTypes(stepTypes);
        }
    }, [stepTypeList]);

    useEffect(() => {
        if (playbook && saveRequested) {
            log.verbose("Saving playbook definition", { playbook });
            props.playbookDefinitionLoader.update(playbook.toApi());
            setDirty(false);
            setSaveRequested(false);
        }
    }, [playbook, saveRequested]);

    function innerSetPlaybook(newPlaybook: PlaybookDefinition) {
        // Trim dangling sequences before updating the playbook.
        // PERF: We could try to do this only in situations that might cause dangling sequences, but it's not worth the effort right now.
        newPlaybook = newPlaybook.trimDanglingSequences();
        log.verbose("Playbook updated", { old: playbook, new: newPlaybook })
        setPlaybook(newPlaybook);
        setDirty(true);
    }

    function savePlaybook() {
        log.verbose("Save requested");
        setSaveRequested(true);
    }

    const value = useMemo(() => ({
        playbook,
        dirty,
        stepTypes,
        setPlaybook: innerSetPlaybook,
        savePlaybook,
    }), [playbook, dirty, stepTypes]);

    if (!stepTypes) return (
        <div className="bg-white m-1 p-2 rounded-xl border border-gray-300">
            <Loading />
        </div>
    );

    return (
        <PlaybookContext.Provider value={value}>
            { props.children }
        </PlaybookContext.Provider>
    );
}