/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { PlatformType } from './PlatformType';

export type Organization = {
    id?: number;
    platformOrganizationId?: string | null;
    platformType?: PlatformType;
    name?: string | null;
    isLocal?: boolean | null;
    avatarUri?: string | null;
};

