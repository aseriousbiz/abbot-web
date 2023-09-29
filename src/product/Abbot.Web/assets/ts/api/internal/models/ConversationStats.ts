/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

export type ConversationStats = {
    countByState?: {
        Unknown?: number;
        New?: number;
        NeedsResponse?: number;
        Overdue?: number;
        Waiting?: number;
        Closed?: number;
        Archived?: number;
        Snoozed?: number;
        Hidden?: number;
    } | null;
    totalCount?: number;
};

