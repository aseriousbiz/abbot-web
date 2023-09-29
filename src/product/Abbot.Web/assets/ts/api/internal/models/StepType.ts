/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { DispatchType } from './DispatchType';
import type { IntegrationType } from './IntegrationType';
import type { StepBranch } from './StepBranch';
import type { StepKind } from './StepKind';
import type { StepPresentation } from './StepPresentation';
import type { StepProperty } from './StepProperty';

export type StepType = {
    name?: string | null;
    kind?: StepKind;
    presentation?: StepPresentation;
    category?: string | null;
    hasUnboundedOutputs?: boolean;
    inputs?: Array<StepProperty> | null;
    readonly outputs?: Array<StepProperty> | null;
    readonly branches?: Array<StepBranch> | null;
    staffOnly?: boolean;
    readonly requiredFeatureFlags?: Array<string> | null;
    readonly requiredIntegrations?: Array<IntegrationType> | null;
    deprecated?: boolean;
    readonly additionalDispatchTypes?: Array<DispatchType> | null;
    readonly requiredTriggers?: Array<string> | null;
};

