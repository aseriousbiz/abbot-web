/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { ProblemDetails } from './ProblemDetails';

export type HttpValidationProblemDetails = (ProblemDetails & {
    errors?: Record<string, Array<string>> | null;
});

