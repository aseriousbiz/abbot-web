/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

export type Room = {
    id?: number;
    isLocal?: boolean | null;
    platformRoomId?: string | null;
    name?: string | null;
    platformUrl?: string | null;
    persistent?: boolean;
    managedConversationsEnabled?: boolean;
};

