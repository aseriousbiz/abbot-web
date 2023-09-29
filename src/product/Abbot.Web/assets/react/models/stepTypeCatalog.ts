import * as Api from "../../ts/api/internal";
import { IntegrationType, PropertyTypeKind } from "./step";
import {DispatchType} from "../../ts/api/internal";

export class StepType {
    constructor(
        public readonly name: string,
        public readonly kind: Api.StepKind,
        public readonly presentation: Api.StepPresentation,
        public readonly category: string,
        public readonly hasUnboundedOutputs: boolean,
        public readonly staffOnly: boolean,
        public readonly inputs: Api.StepProperty[],
        public readonly outputs: Api.StepProperty[],
        public readonly branches: Api.StepBranch[],
        public readonly requiredFeatureFlags: string[],
        public readonly requiredIntegrations: IntegrationType[],
        public readonly deprecated: boolean,
        public readonly additionalDispatchTypes: DispatchType[],
        public readonly requiredTriggers: string[] = []) {
    }

    /** Gets a name appropriate for using as the name of a step of this type. */
    getStepName(): string {
        return this.name.replace(/\.|-|:/g, '_');
    }

    hasMatchingOutput(name: string, kind: PropertyTypeKind): boolean {
        return this.hasUnboundedOutputs ||
            this.outputs.some(o => o.name === name && o.type.kind == kind);
    }
}

export class StepTypeCatalog {
    private readonly _types: Record<string, StepType> = {};
    constructor(public readonly activeFeatureFlags: string[], public readonly enabledIntegrations: IntegrationType[]) {
    }

    public get types(): StepType[] {
        return Object.values(this._types);
    }

    hasFeatureFlags(stepType: StepType): boolean {
        return stepType.requiredFeatureFlags.every((flag) => this.activeFeatureFlags.includes(flag));
    }

    hasIntegrations(stepType: StepType): boolean {
        return stepType.requiredIntegrations.every((integration) => this.enabledIntegrations.includes(integration));
    }

    getType(typeName: string): StepType | null {
        return this._types[typeName] || null;
    }

    addType(type: StepType) {
        if (type.name in this._types) {
            throw new Error(`Duplicate step type: ${type.name}`);
        }
        this._types[type.name] = type;
    }

    static fromApi(list: Api.StepTypeList) {
        const catalog = new StepTypeCatalog(list.activeFeatureFlags || [], list.enabledIntegrations || []);
        list.stepTypes.forEach((type) => {
            // Resolve the type into an object.
            const resolvedType = new StepType(
                type.name,
                type.kind,
                type.presentation,
                type.category,
                !!type.hasUnboundedOutputs,
                type.staffOnly,
                type.inputs || [],
                type.outputs || [],
                type.branches || [],
                type.requiredFeatureFlags || [],
                type.requiredIntegrations || [],
                type.deprecated || false,
                type.additionalDispatchTypes || [],
                type.requiredTriggers || []);
            catalog.addType(resolvedType);
        });
        return catalog;
    }
}