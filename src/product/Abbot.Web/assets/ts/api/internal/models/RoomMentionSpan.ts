/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { RenderedMessageSpan } from './RenderedMessageSpan';
import type { Room } from './Room';

export type RoomMentionSpan = (RenderedMessageSpan & {
    platformRoomId?: string | null;
    room?: Room;
});

