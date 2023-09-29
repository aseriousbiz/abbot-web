/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { PlaybookDefinition } from './PlaybookDefinition';

export type CreateVersionRequestModel = {
    /**
     * Gets or sets a description of the changes in this version.
     */
    comment?: string | null;
    publishedAt?: string | null;
    definition?: PlaybookDefinition;
};

