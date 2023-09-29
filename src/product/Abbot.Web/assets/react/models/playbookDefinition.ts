import * as Api from "../../ts/api/internal";
import { StepType, StepTypeCatalog } from "./stepTypeCatalog";
import {
    Step,
    StepSequence,
    StepLocation,
    TriggerSequence,
    SequenceId,
    IdentifiedStep,
    SavedStep,
    PropertyTypeKind,
    PlacedStep
} from "./step";
import logger from "../../ts/log";
import { DispatchSettings } from "./dispatch";
import {Option, OptionsDefinition} from "./options";

const log = logger('playbookDefinition');

/** Creates an IdentifiedStep from the raw Playbook Definition retrieved from the API. */
function resolveStep(step: Api.Step | Api.ActionStep, kind: Api.StepKind, stepTypes: StepTypeCatalog): IdentifiedStep {
    let type = stepTypes.getType(step.type);
    if (!type) {
        // The playbook definition has a step we don't recognize.
        // This can happen if the definition was created on a different version of the app.
        log.error(`Step ${step.id} has unknown type ${step.type}`);
        type = new StepType(
            step.id,
            kind,
            {
                icon: "fa-circle-question",
                label: "Unknown Step",
                description: "This step is no longer supported. Delete it and add a replacement.",
            },
            "unknown",
            false,
            false,
            Object.keys(step.inputs).map(name => ({
                name,
                title: name,
                type: { kind: 'String' },
            })), [], [], [], [], false, []);
    }

    return new Step(step.id, undefined, type, step.inputs, "branches" in step ? step.branches : {});
}

function collectStepIds(triggers: StepSequence, sequences: Record<string, StepSequence>): Set<string> {
    return new Set<string>([
        ...(triggers.steps.map(s => s.id)),
        ...(Object.values(sequences).flatMap(s => s.steps.map(s => s.id))),
    ]);
}

function generateId(baseName: string, existingIds: Set<string>): string {
    // Check if that's unique
    if (!existingIds.has(baseName)) {
        return baseName;
    }

    // If it's not, add incrementing numbers to the end until it is
    let i = 1;
    let candidate;
    do {
        candidate = `${baseName}_${i}`;
        i++;
    } while (i < 100 && existingIds.has(candidate))

    if (i >= 100) {
        throw new Error(`Could not generate a unique ID for ${baseName} after 100 attempts.`);
    }
    return candidate;
}

/** An immutable definition of a playbook */
export class PlaybookDefinition {
    private constructor(
        public readonly webhookTriggerUrl: string,
        public readonly triggers: StepSequence,
        public readonly dispatchSettings: DispatchSettings,
        public readonly startSequence: string,
        public readonly sequences: Record<string, StepSequence>,
        public readonly formatVersion: number,
        readonly allStepIDs: Set<string>,
        readonly allSequenceIDs: Set<string>) {
    }

    public toString(): string {
        return JSON.stringify(this.toApi(), null, 2);
    }

    /**
     * Returns the trigger if the trigger for this Playbook has an output of the specified name and
     * {@link PropertyTypeKind}
     * @param outputName The name of the output to check.
     * @param outputKind The {@link PropertyTypeKind} that the output must have to match.
     */
    public getTriggerWithOutput(outputName: string, outputKind: PropertyTypeKind): PlacedStep | undefined {
        function dispatchIsMatch(dispatchSettings: DispatchSettings) {
            if (dispatchSettings.type !== "ByCustomer") {
                return false;
            }
            return (outputName === "channel" && outputKind === "Channel") ||
                (outputName === "customer" && outputKind === "Customer");
        }

        // We only support one trigger now.
        const trigger = this.triggers.steps[0];

        return trigger && trigger.type.hasMatchingOutput(outputName, outputKind) || dispatchIsMatch(this.dispatchSettings)
            ? trigger
            : undefined;
    }

    /**
     * Determines if a preceding step _in the same sequence_, or a trigger, has an output of the specified name and
     * {@link PropertyTypeKind} returns the step with the output if it does.
     * @param location The current location.
     * @param outputName The name of the output to check.
     * @param outputKind The {@link PropertyTypeKind} that the output must have to match.
     */
    public getActionStepWithOutput(location: StepLocation, outputName: string, outputKind: PropertyTypeKind): PlacedStep | undefined {
        const sequence = this.getSequenceFromLocation(location);
        const result = this.getActionStepWithOutputInSequence(sequence, location, outputName, outputKind, false);
        if (sequence.id === this.startSequence) {
            return result;
        }

        // Right now we only allow one level of nesting, so we can just check the start sequence.
        const startSequence = this.sequences[this.startSequence];
        const parentLocation = this.getParentLocation(location, startSequence);
        if (parentLocation) {
            return this.getActionStepWithOutputInSequence(startSequence, parentLocation, outputName, outputKind, true);
        }
    }

    /*
     * If the current step type requires specific triggers, and the triggers are not there, this returns the
     * missing trigger types.
     */
    public hasRequiredTriggers(stepType: StepType, stepTypes: StepTypeCatalog) : {requiredTriggersExist: boolean, requiredTriggers: StepType[]} {
        const requiredTriggers = stepType.requiredTriggers.map(name => stepTypes.getType(name));

        if (stepType.requiredTriggers.length === 0) {
            return { requiredTriggersExist: true, requiredTriggers};
        }

        const trigger = this.triggers.steps[0];
        if (!trigger
            || stepType.requiredTriggers.length > 0 && !stepType.requiredTriggers.includes(trigger.type.name)) {

            return { requiredTriggersExist: false, requiredTriggers };
        }

        return { requiredTriggersExist: true, requiredTriggers };
    }

    /*
     * Get the step at the specified location.
     */
    public getStepFromLocation(location: StepLocation) : PlacedStep {
        if (!location) {
            return undefined;
        }
        const sequence = this.getSequenceFromLocation(location);
        return sequence?.steps[location.index];
    }

    public getParentLocation(location: StepLocation, sequence: StepSequence): StepLocation | undefined {
        if (sequence.steps) {
            const step = sequence.steps.find(step => {
                if (step.branches) {
                    for (const branch of Object.values(step.branches)) {
                        if (branch === location.sequence) {
                            return true;
                        }
                    }
                }
            });
            return step?.location;
        }
        return;
    }

    public getActionStepWithOutputInSequence(
        sequence: StepSequence,
        location: StepLocation,
        outputName: string,
        outputKind: PropertyTypeKind,
        inclusive: boolean): PlacedStep | undefined {

        if (sequence.steps.length <= 0) {
            return undefined;
        }

        // Three scenarios here:
        // * The step is brand new and appearing at the end of the sequence. Index will be undefined in this case, and we search the entire sequence
        //      (sequence.steps.length - 1)
        // * The step is brand new and appearing before an existing step. Index points at the step we're being inserted _BEFORE_, so we search starting at the step BEFORE that step.
        //      (location.index - 1)
        // * The step is an existing one being edited. Index points to the step we're editing, so we search starting at the step BEFORE that step.
        //      (location.index - 1)
        let currentIndex = location.index === undefined
            ? sequence.steps.length - 1
            : (inclusive ? location.index : location.index - 1);
        while (currentIndex >= 0) {
            const step = this.getStep(location.sequence, currentIndex);
            if (step.type.hasMatchingOutput(outputName, outputKind)) {
                return step;
            }
            if (step.type.branches) {
                // If the step has branches, we need to check if any of them have the output
                const branchNames = step.type.branches.map(b => b.name);
                for (const branchName of branchNames) {
                    const branchSequence = this.sequences[`${step.id}:${branchName}`];

                    const branchStepWithOutput = branchSequence?.steps.find(s => s.type.hasMatchingOutput(outputName, outputKind));
                    if (branchStepWithOutput) {
                        return branchStepWithOutput;
                    }
                }
            }
            currentIndex--;
        }

        return undefined;
    }

    /**
     * Determines if a preceeding step _in the same sequence_, or a trigger, has an output of the specified name and {@link PropertyTypeKind}
     * @param location The current location.
     * @param outputName The name of the output to check.
     * @param outputKind The {@link PropertyTypeKind} that the output must have to match.
     */
    public getOutputAvailableAt(location: StepLocation, outputName: string, outputKind: PropertyTypeKind): PlacedStep | undefined {
        return this.getActionStepWithOutput(location, outputName, outputKind)
            ?? this.getTriggerWithOutput(outputName, outputKind);
    }

    public findPrecedingStep(location: StepLocation, stepTypeName: string): SavedStep | null {
        const sequence = this.getSequenceFromLocation(location);
        if (sequence.steps.length <= 0) {
            return null;
        }

        let currentIndex = location.index === undefined ? sequence.steps.length - 1 : location.index - 1;
        while (currentIndex >= 0) {
            const step = this.getStep(location.sequence, currentIndex);
            if (step.type.name === stepTypeName) {
                return step;
            }
            currentIndex--;
        }
        return null;
    }

    public findPrecedingPollOptions(location: StepLocation): Option[] | null {
        const pollStep = this.findPrecedingStep(location, 'slack.send-poll');
        if (pollStep) {
            // Build a drop down from pollStep.inputs.options
            const optionsDefinition = pollStep.inputs.options as OptionsDefinition;
            return optionsDefinition.options as Option[];
        }
        return null;
    }

    /* Returns true if the sequence directly contains a step of the given type. */
    public sequenceHasStep(sequenceId: string, stepTypeName: string) {
        return (!!this.sequences[sequenceId].steps.find(s => s.type.name === stepTypeName));
    }

    private getSequenceFromLocation(location: StepLocation): StepSequence {
        return location.sequence === TriggerSequence
            ? this.triggers
            : this.sequences[location.sequence];
    }

    public toApi(): Api.PlaybookDefinition {
        const convertedTriggers: Array<Api.Step> | null = this.triggers.toApi().actions;

        const convertedSequences: Record<string, Api.ActionSequence> = {};
        Object.entries(this.sequences).forEach(([key, value]) => {
            convertedSequences[key] = value.toApi();
        });

        // Order is important for dirty detection
        return {
            triggers: convertedTriggers,
            dispatch: this.dispatchSettings,
            sequences: convertedSequences,
            formatVersion: this.formatVersion == 0 ? undefined : this.formatVersion,
            startSequence: this.startSequence,
        };
    }

    /**
     * Creates a new PlaybookDefinition with the provided dispatch settings.
     * 
     * @param updatedSettings The updated {@link DispatchSettings}.
     * @returns A PlaybookDefinition with the provided dispatch settings.
     */
    public withDispatchSettings(updatedSettings: DispatchSettings): PlaybookDefinition {
        return new PlaybookDefinition(
            this.webhookTriggerUrl,
            this.triggers,
            updatedSettings,
            this.startSequence,
            this.sequences,
            this.formatVersion,
            this.allSequenceIDs,
            this.allStepIDs);
    }

    /**
     * Generates a brand new step ID that is unique within this playbook.
     * If the provided {@link baseName} is already unique, it is returned.
     * Otherwise, an incrementing number, starting at 1, is appended to the end of the name until it is unique.
     * @param baseName The requested ID
     * @returns The unique ID
     */
    public generateStepId(baseName: string): string {
        return generateId(baseName, this.allStepIDs);
    }

    /**
     * Generates a brand new sequence ID that is unique within this playbook.
     * If the provided {@link baseName} is already unique, it is returned.
     * Otherwise, an incrementing number, starting at 1, is appended to the end of the name until it is unique.
     * @param baseName The requested ID
     * @returns The unique ID
     */
    public generateSequenceId(baseName: string): string {
        return generateId(baseName, this.allSequenceIDs);
    }

    /**
     * Adds a new empty {@link StepSequence} with the specified ID to the playbook.
     * @param id The ID of the new sequence
     * @returns A new PlaybookDefinition with the sequence added
     */
    public addSequence(id: string): PlaybookDefinition {
        if (id in this.sequences) {
            throw new Error(`Sequence ${id} already exists`);
        }

        const newSequenceIds = new Set<string>(this.allSequenceIDs);
        newSequenceIds.add(id);
        return new PlaybookDefinition(
            this.webhookTriggerUrl,
            this.triggers,
            this.dispatchSettings,
            this.startSequence,
            {
                ...this.sequences,
                [id]: StepSequence.create(id),
            },
            this.formatVersion,
            this.allStepIDs,
            newSequenceIds);
    }

    /**
     * Creates a new PlaybookDefinition with the provided step removed.
     * @param step The step to remove
     * @returns A new PlaybookDefinition with the step removed
     */
    public removeStep(step: SavedStep): PlaybookDefinition {
        const newStepIds = new Set<string>(this.allStepIDs);
        newStepIds.delete(step.id);
        if (step.location.isAction()) {
            const sequence = this.sequences[step.location.sequence];
            if (!sequence) {
                throw new Error(`Sequence '${step.location.sequence}' not found`);
            }
            const newSequence = sequence.withoutStep(step.id);
            return this.replaceSequence(newSequence, newStepIds);
        }
        else {
            const newTriggers = this.triggers.withoutStep(step.id);
            return this.replaceSequence(newTriggers, newStepIds);
        }
    }

    /**
     * Creates a new PlaybookDefinition with the provided step added.
     * @param step The step to add
     * @param location The location to add the step at
     * @returns A new PlaybookDefinition with the step added.
     */
    public addStep(step: Step, location: StepLocation): PlaybookDefinition {
        if (this.allStepIDs.has(step.id)) {
            throw new Error(`Step ${step.id} already exists`);
        }

        const newStepIds = new Set<string>(this.allStepIDs);
        newStepIds.add(step.id);
        if (location.isAction()) {
            if (step.type.kind !== 'Action') {
                throw new Error(`Cannot add a ${step.type.kind} as an action`);
            }

            const oldSequence = this.sequences[location.sequence];
            if (!oldSequence) {
                throw new Error(`Sequence '${location.sequence}' not found`);
            }
            const sequence = oldSequence.withStep(step, location.index);
            return this.replaceSequence(sequence, newStepIds);
        } else {
            if (step.type.kind !== 'Trigger') {
                throw new Error(`Cannot add a ${step.type.kind} as an trigger`);
            }

            const sequence = this.triggers.withStep(step, location.index);
            return this.replaceSequence(sequence, newStepIds);
        }
    }

    /**
     * Creates a new PlaybookDefinition with the provided step moved to the provided location.
     * 
     * Steps below the original step location are moved up, and steps below the new location are moved down.
     * 
     * @param step The step to move.
     * @param location The new location of the step.
     * @returns A new PlaybookDefinition with the step moved.
     */
    public moveStep(step: SavedStep, location: StepLocation): PlaybookDefinition {
        return this.removeStep(step).addStep(step, location);
    }

    /**
     * Creates a new PlaybookDefinition with the provided step replaced with a new step.
     * 
     * The step is replaced in the same location as the original step.
     * 
     * @param step The step to replace
     * @param newStep The new step to replace it with
     * @returns A new PlaybookDefinition with the step replaced.
     */
    public replaceStep(step: SavedStep, newStep: Step): PlaybookDefinition {
        return this.removeStep(step).addStep(newStep, step.location);
    }


    /**
     * Gets the step at the provided {@link StepLocation}
     * @param sequenceId The ID of the sequence to search
     * @param index The Index of the step to find
     * @returns The step, or null if it does not exist.
     */
    public getStep(sequenceId: SequenceId, index: number): SavedStep | null;

    /**
     * Gets the step with the provided ID in the provided sequence.
     * @param sequenceId The ID of the sequence to search
     * @param id The ID of the step to find
     * @returns The step, or null if it does not exist.
     */
    public getStep(sequenceId: SequenceId, id: string): SavedStep | null;
    public getStep(sequenceId: SequenceId, idOrIndex: string | number): SavedStep | null {
        if (typeof idOrIndex === "number") {
            const sequence = sequenceId === TriggerSequence ? this.triggers : this.sequences[sequenceId];
            return sequence.steps[idOrIndex];
        } else if(typeof idOrIndex === "string") {
            const sequence = sequenceId === TriggerSequence ? this.triggers : this.sequences[sequenceId];
            if (!sequence) {
                return null;
            }
            return sequence.getStep(idOrIndex);
        } else {
            throw new Error("Invalid argument");
        }
    }

    /**
     * Trims dangling sequences from the playbook.
     * 
     * A dangling sequence is one that is:
     * * NOT the start sequence
     * * NOT referenced in any 'branches' list of any step.
     * 
     * @returns A new PlaybookDefinition with the dangling sequences removed.
     */
    public trimDanglingSequences(): PlaybookDefinition {
        const reachableSequences = new Set<string>([this.startSequence]);
        const queue = [this.startSequence];
        while(queue.length > 0) {
            const sequenceId = queue.shift();

            // Scan for reachable sequences
            this.sequences[sequenceId].steps.forEach(step => {
                Object.values(step.branches).forEach(branch => {
                    if (!reachableSequences.has(branch)) {
                        reachableSequences.add(branch);
                        queue.push(branch);
                    }
                });
            });
        }

        // Only preserve reachable sequences
        const newSequences: Record<string, StepSequence> = {};
        Object.entries(this.sequences).forEach(([key, value]) => {
            if (reachableSequences.has(key)) {
                newSequences[key] = value;
            }
        });
        return new PlaybookDefinition(
            this.webhookTriggerUrl,
            this.triggers,
            this.dispatchSettings,
            this.startSequence,
            newSequences,
            this.formatVersion,
            this.allStepIDs,
            new Set<string>(Object.keys(newSequences)));
    }

    /**
     * Resolves a PlaybookDefinition from the provided API definition, and the provided StepTypeCatalog.
     */
    public static resolve(playbookDefinition: Api.PlaybookDefinition, stepTypes: StepTypeCatalog, webhookTriggerUrl: string) : PlaybookDefinition {
        const triggers = StepSequence.create(
            TriggerSequence,
            playbookDefinition.triggers.map(trigger => resolveStep(trigger, "Trigger", stepTypes)));

        const sequences: Record<string, StepSequence> | null = {};
        if (playbookDefinition.sequences) {
            for (const [key, value] of Object.entries(playbookDefinition.sequences)) {
                const actions = value.actions?.map(action => resolveStep(action, "Action", stepTypes));
                sequences[key] = StepSequence.create(key, actions);
            }
        }

        const definition = new PlaybookDefinition(
            webhookTriggerUrl,
            triggers,
            playbookDefinition.dispatch || {
                type: "Once",
            },
            playbookDefinition.startSequence,
            sequences,
            playbookDefinition.formatVersion || 0,
            collectStepIds(triggers, sequences),
            new Set<string>(Object.keys(sequences)));

        return definition.withBranchesMaterialized();
    }

    private withBranchesMaterialized(): PlaybookDefinition {
        // Walk all the sequences and steps looking for branches that aren't materialized
        // A branch is 'materialized' if it:
        // * Has a 'sequenceId' associated with it
        // * That sequenceId is in the sequences list

        // This is a little clunkier than I wanted ('let definition = this', and then updating 'definition'), but it works.
        // I wanted to collect up all the changes to be made and then apply them all at once at the end,
        // but this was way easier so I punted on that idea.
        // eslint-disable-next-line @typescript-eslint/no-this-alias
        let definition: PlaybookDefinition = this;
        Object.values(this.sequences).flatMap((value) => value.steps).forEach(step => {
            const allBranches: Record<string, string> = {};
            let updated = false;
            step.type.branches.forEach(branch => {
                const sequenceId = step.branches?.[branch.name];
                const sequence = sequenceId ? this.sequences[sequenceId] : undefined;
                if (!sequence) {
                    // No sequence, or it hasn't been materialized yet
                    const newSequenceId = this.generateSequenceId(`${step.id}:${branch.name}`);
                    log.verbose(`Materializing sequence ${newSequenceId} for branch ${branch.name} of step ${step.id}`);
                    definition = definition.addSequence(newSequenceId);
                    updated = true;
                    allBranches[branch.name] = newSequenceId;
                } else {
                    allBranches[branch.name] = sequenceId;
                }
            });
            if (updated) {
                definition = definition.replaceStep(step, step.withBranches(allBranches));
            }
        });
        return definition;
    }

    private replaceSequence(updatedSequence: StepSequence, updatedStepIds: Set<string>): PlaybookDefinition {
        if (updatedSequence.id === TriggerSequence) {
            return new PlaybookDefinition(
                this.webhookTriggerUrl,
                updatedSequence,
                this.dispatchSettings,
                this.startSequence,
                this.sequences,
                this.formatVersion,
                updatedStepIds,
                this.allSequenceIDs);
        } else {
            return new PlaybookDefinition(
                this.webhookTriggerUrl,
                this.triggers,
                this.dispatchSettings,
                this.startSequence,
                {
                    ...this.sequences,
                    [updatedSequence.id]: updatedSequence,
                },
                this.formatVersion,
                updatedStepIds,
                this.allSequenceIDs);
        }
    }
}
