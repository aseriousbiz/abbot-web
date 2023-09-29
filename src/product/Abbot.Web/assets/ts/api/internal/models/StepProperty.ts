/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { PropertyType } from './PropertyType';

export type StepProperty = {
    name?: string | null;
    title?: string | null;
    type?: PropertyType;
    required?: boolean;
    description?: string | null;
    expressionContext?: string | null;
    default?: any;
    placeholder?: string | null;
    hidden?: boolean;
    oldNames?: Array<string> | null;
};

