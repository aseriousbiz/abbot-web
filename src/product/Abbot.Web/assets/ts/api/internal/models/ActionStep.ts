/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { Step } from './Step';

export type ActionStep = (Step & {
    readonly branches?: Record<string, string> | null;
});

