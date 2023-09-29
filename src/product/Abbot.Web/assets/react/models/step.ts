import * as Api from "../../ts/api/internal";
import { StepType } from "./stepTypeCatalog";
import logger from "../../ts/log";

const log = logger("step");

// Re-export Api types that need no modification
export type StepPresentation = Api.StepPresentation;
export type StepProperty = Api.StepProperty;
export type StepBranch = Api.StepBranch;
export type StepKind = Api.StepKind;
export type PropertyType = Api.PropertyType;
export type PropertyTypeKind = Api.PropertyTypeKind;
export type DispatchSettings = Api.DispatchSettings;
export type DispatchType = Api.DispatchType;
export type IntegrationType = Api.IntegrationType;

/** The Sequence ID for the set of triggers in the playbook */
export const TriggerSequence: unique symbol = Symbol("triggers");

/** A Sequence ID is either the special value {@link TriggerSequence}, or a string naming a sequence in the playbook. */
export type SequenceId = typeof TriggerSequence | string;

/** Gets a descriptive name for a sequence ID */
function getSequenceName(id: SequenceId): string {
    return id === TriggerSequence ? "$triggers" : id;
}

/** Represents a reference to a step's location in the Playbook. */
export class StepLocation {
    /**
     * Constructs a new StepLocation.
     * 
     * @param sequence The ID of the {@link StepSequence} containing the step.
     * @param index The index of this step in the sequence. If undefined, the step is not yet placed and is targeted at the end of the sequence.
     */
    constructor(
        public readonly sequence: SequenceId,
        public readonly index?: number) {
    }

    /** Returns a boolean indicating if this location refers to an Action Sequence
     * 
     * If false, this location refers to a trigger.
     * @returns A boolean indicating if this location refers to an Action Sequence
     */
    isAction(): this is StepLocation & { sequence: string } {
        return this.sequence !== TriggerSequence;
    }

    toString(): string {
        const indexStr = (!this.index && this.index !== 0) ? "<end>" : `${this.index}`;
        return `${getSequenceName(this.sequence)}#${indexStr}`;
    }

    static equal(left?: StepLocation, right?: StepLocation) {
        return left?.sequence === right?.sequence && left?.index === right?.index;
    }
}

/**
 * Represents a Step in the Playbook.
 * 
 * The Step MAY NOT have been placed in the Playbook yet,
 * the presence of the {@link id} and {@link location} properties indicate the state of the step.
 * 
 * If the {@link location} is undefined, the step has not been assigned a location in the Playbook.
 * If the {@link id} is undefined, the step has not yet been saved to the Playbook and assigned an ID.
 * 
 * The lifecycle of a step depends on how it comes into being.
 * 
 * If a step is loaded from the playbook definition, we know it's ID and location, so both properties are set.
 * When a step is added, we know the location we're adding it to, but not the ID, so only the location is set.
 * The playbook editor can use the presence of the ID to determine if a step needs to be saved.
 */
export class Step {
    constructor(
        public readonly id: string | undefined,
        public readonly location: StepLocation | undefined,
        public readonly type: StepType,
        public readonly inputs: Record<string, unknown>,
        public readonly branches?: Record<string, string>) {
        // Replace renamed inputs with their preferred name
        type.inputs.forEach(typeInput => {
            typeInput.oldNames?.forEach(oldName => {
                if (oldName in inputs) {
                    const newName = typeInput.name;
                    log.log(`Renaming property '${oldName}' to '${newName}'`);
                    inputs[newName] = inputs[oldName];
                    delete inputs[oldName];
                }
            });
        });
    }

    /**
     * Creates a new {@link Step} with no ID, inputs, or branches specified.
     * @param type The type of the step to create.
     * @param location The location of the step to create.
     * @returns A new Step
     */
    static createNew(type: StepType, location: StepLocation): Step {
        return new Step(undefined, location, type, {}, {});
    }

    /**
     * Returns a new Step with {@link location} set to the provided value.
     * 
     * @param location The new location for the step.
     * @returns An updated Step
     */
    placedAt(location: StepLocation): PlacedStep {
        return new Step(this.id, location, this.type, this.inputs, this.branches);
    }

    /**
     * Returns a new Step with {@link id} set to the provided value.
     * 
     * @param id The new ID for the step.
     * @returns An updated Step
     */
    withId(id: string): IdentifiedStep {
        return new Step(id, this.location, this.type, this.inputs, this.branches);
    }

    /**
     * Returns a new Step with {@link inputs} set to the provided value.
     * 
     * @param inputs The new inputs for the step.
     * @returns An updated Step
     */
    withInputs(inputs: Record<string, unknown>): Step {
        return new Step(this.id, this.location, this.type, inputs, this.branches);
    }

    /**
     * Returns a new Step with {@link branches} set to the provided value.
     * 
     * @param branches The new branches for the step.
     * @returns An updated Step
     */
    withBranches(branches: Record<string, string>): Step {
        return new Step(this.id, this.location, this.type, this.inputs, branches);
    }

    get uniqueId(): string {
        if (this.location && this.id) {
            return `${getSequenceName(this.location.sequence)}/${this.id}`;
        } else {
            throw new Error("Cannot get unique ID for step without ID");
        }
    }

    /** Generates the payload for the step expected by the API. */
    toApi(): Api.Step | Api.ActionStep {
        if (this.type.kind === "Action" && this.branches) {
            return {
                id: this.id,
                type: this.type.name,
                inputs: this.inputs,
                branches: this.branches,
            };
        } else {
            return {
                id: this.id,
                type: this.type.name,
                inputs: this.inputs,
            };
        }
    }

    /** Checks if the provided value is a {@link Step}. */
    static is(a: unknown): a is Step {
        return typeof a === "object" && a instanceof Step;
    }

    toString(): string {
        const id = this.id ?? "<unidentified>";
        const location = this.location?.toString() ?? "<unplaced>";
        return `${id} @ ${location}`;
    }
}

/** A type storing values that may be updated on a {@link Step} */
export type StepUpdate = Partial<Pick<Step, "inputs" | "branches">>;

/** Represents a {@link Step} that has a known ID. */
export type IdentifiedStep = Step & { id: string };

/** Represents a {@link Step} that has a known location. */
export type PlacedStep = Step & { location: StepLocation };

/** Represents a {@link Step} that has a known ID and location. */
export type SavedStep = PlacedStep & IdentifiedStep;

/**
 * Inserts the provided step into the provided array and updates the {@link Step.location} property of the step as appropriate
 * 
 * @param step The step to insert
 * @param sequenceId The sequence represented by the array in {@link array}, used to update {@link Step.location}
 * @param array The array to insert the step into
 */
function placeStep(step: Step, sequenceId: SequenceId, array: PlacedStep[]): PlacedStep {
    const index = array.length;
    const placed = step.placedAt(new StepLocation(sequenceId, index));
    array.push(placed);
    return placed;
}

/**
 * Represents a sequence of {@link Step}s, which may be either Actions or Triggers.
 * 
 * This type is immutable. All methods that modify the sequence return a new instance.
 */
export class StepSequence {
    private constructor(public readonly id: SequenceId, public readonly steps: PlacedStep[]) {
    }

    /** Creates a new sequence given the provided ID and steps. */
    static create(id: SequenceId, steps?: IdentifiedStep[]) {
        const placedSteps: PlacedStep[] = [];
        if (steps) {
            steps.forEach(s => placeStep(s, id, placedSteps));
        }
        return new StepSequence(id, placedSteps);
    }

    /** Generates the payload for the sequence expected by the API. */
    toApi(): Api.ActionSequence {
        const actions = this.steps.map(a => a.toApi());
        return {
            actions,
        };
    }

    /** Attempts to locate the step with the provided ID, returning null if no step can be found. */
    getStep(id: string): SavedStep | null {
        return this.steps.find(s => s.id === id);
    }

    /** 
     * Returns a new sequence with the provided step replacing the step with the provided ID. 
     * 
     * @param id The ID of the {@link Step} to replace.
     * @param newStep The new {@link Step} to insert.
     * @returns A new sequence with the step replaced.
    */
    withStepReplaced(id: string, newStep: Step): StepSequence {
        const newSteps: PlacedStep[] = [];
        this.steps.forEach(element => {
            if (element.id === id) {
                placeStep(newStep, this.id, newSteps);
            } else {
                placeStep(element, this.id, newSteps);
            }
        });
        return new StepSequence(this.id, newSteps);
    }

    /**
     * Returns a new sequence with the step with the provided ID removed.
     * @param id The ID of the {@link Step} to remove.
     * @returns A new sequence with the step removed.
     */
    withoutStep(id: string): StepSequence {
        const newSteps: PlacedStep[] = [];
        this.steps.forEach(element => {
            if (element.id !== id) {
                placeStep(element, this.id, newSteps);
            }
        });
        return new StepSequence(this.id, newSteps);
    }

    /**
     * Returns a new sequence with the provided step inserted at the end.
     * @param step The {@link Step} to insert
     * @returns A new sequence with the step inserted.
     */
    withStep(step: Step): StepSequence;

    /**
     * Returns a new sequence with the provided step inserted at the provided index.
     * @param step The {@link Step} to insert
     * @param targetIndex The index to insert the step at.
     * @returns A new sequence with the step inserted.
     */
    withStep(step: Step, targetIndex: number): StepSequence;
    withStep(step: Step, targetIndex?: number): StepSequence {
        const newSteps: PlacedStep[] = [];

        let placed: PlacedStep = null;
        this.steps.forEach((element, index) => {
            if (placed === null && index === targetIndex) {
                placed = placeStep(step, this.id, newSteps);
            }
            placeStep(element, this.id, newSteps);
        });
        if (placed === null) {
            placed = placeStep(step, this.id, newSteps);
        }
        return new StepSequence(this.id, newSteps);
    }
}