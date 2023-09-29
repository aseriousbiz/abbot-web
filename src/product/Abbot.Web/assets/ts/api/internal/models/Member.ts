/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { Organization } from './Organization';
import type { WorkingHours } from './WorkingHours';

export type Member = {
    id?: number;
    isLocal?: boolean | null;
    nickName?: string | null;
    searchKey?: string | null;
    userName?: string | null;
    platformUserId?: string | null;
    avatarUrl?: string | null;
    timeZoneId?: string | null;
    organization?: Organization;
    workingHours?: WorkingHours;
    workingHoursInYourTimeZone?: WorkingHours;
};

