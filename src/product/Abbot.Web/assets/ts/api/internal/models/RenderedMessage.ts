/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { RoomMentionSpan } from './RoomMentionSpan';
import type { TextSpan } from './TextSpan';
import type { UserMentionSpan } from './UserMentionSpan';

export type RenderedMessage = {
    text?: string | null;
    spans?: Array<(UserMentionSpan | RoomMentionSpan | TextSpan)> | null;
};

