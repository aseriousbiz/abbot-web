/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { ConversationMember } from './ConversationMember';
import type { Member } from './Member';
import type { RenderedMessage } from './RenderedMessage';
import type { Room } from './Room';

export type Conversation = {
    id?: number;
    title?: RenderedMessage;
    lastMessagePostedOn?: string;
    firstMessageUrl?: string | null;
    startedBy?: Member;
    room?: Room;
    members?: Array<ConversationMember> | null;
};

