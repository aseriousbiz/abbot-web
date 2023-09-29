/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { ConversationVolumePeriod } from './ConversationVolumePeriod';

export type ConversationVolume = {
    timeZone?: string | null;
    data?: Array<ConversationVolumePeriod> | null;
};

