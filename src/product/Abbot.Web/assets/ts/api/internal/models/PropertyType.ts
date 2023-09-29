/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { PropertyTypeKind } from './PropertyTypeKind';
import type { UIHint } from './UIHint';

export type PropertyType = {
    kind?: PropertyTypeKind;
    allowMultiple?: boolean;
    hint?: UIHint;
    editRows?: number | null;
};

