/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { TrendsDay } from './TrendsDay';
import type { TrendsSummary } from './TrendsSummary';

export type Trends = {
    timeZone?: string | null;
    summary?: TrendsSummary;
    data?: Array<TrendsDay> | null;
};

