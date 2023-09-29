/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { Member } from './Member';
import type { RenderedMessageSpan } from './RenderedMessageSpan';

export type UserMentionSpan = (RenderedMessageSpan & {
    platformUserId?: string | null;
    member?: Member;
});

