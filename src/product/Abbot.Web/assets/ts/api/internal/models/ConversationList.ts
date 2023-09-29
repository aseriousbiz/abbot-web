/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { Conversation } from './Conversation';
import type { ConversationStats } from './ConversationStats';
import type { Pagination } from './Pagination';

export type ConversationList = {
    conversations?: Array<Conversation> | null;
    stats?: ConversationStats;
    pagination?: Pagination;
};

