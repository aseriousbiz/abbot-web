/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { IntegrationType } from './IntegrationType';
import type { StepType } from './StepType';

export type StepTypeList = {
    /**
     * Gets all the step types known to the system, INCLUDING those that are not visible to the current user because of staff mode and feature flags.
     */
    stepTypes?: Array<StepType> | null;
    /**
     * Gets a list of all the feature flags referenced by step types that are active for the current user.
     */
    activeFeatureFlags?: Array<string> | null;
    /**
     * Gets a list of all the integrations enabled for the organization.
     */
    enabledIntegrations?: Array<IntegrationType> | null;
};

