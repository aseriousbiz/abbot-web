/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { ActionSequence } from './ActionSequence';
import type { DispatchSettings } from './DispatchSettings';
import type { TriggerStep } from './TriggerStep';

export type PlaybookDefinition = {
    formatVersion?: number;
    readonly triggers?: Array<TriggerStep> | null;
    dispatch?: DispatchSettings;
    startSequence?: string | null;
    readonly sequences?: Record<string, ActionSequence> | null;
};

