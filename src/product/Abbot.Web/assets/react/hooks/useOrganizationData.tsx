import { PropsWithChildren, createContext, useContext, useMemo } from "react";
import { useFetch } from "usehooks-ts";
import { Member, Room } from "../../ts/api/internal";
import { appInsights } from "../../ts/telemetry";
import logger from "../../ts/log";

const log = logger("useOrganizationData");

export interface DataWithError<T> {
    data?: T,
    error?: Error,
}

export interface OrganizationDataContextState {
    channels?: DataWithError<Room[]>,
    members?: DataWithError<Member[]>,
}

export const OrganizationDataContext = createContext(null as OrganizationDataContextState | null);

export default function useOrganizationData() {
    const context = useContext(OrganizationDataContext);
    if (!context) throw new Error("ChannelsContext not found!");
    return context;
}

export function useMembers(memberIds: string[]): { members?: Member[], error?: Error } {
    const { members } = useOrganizationData();
    return useMemo(() => ({
        error: members.error,
        members: memberIds.map(memberId =>
            members.data?.find(c => c.platformUserId === memberId)
            ?? { platformUserId: memberId, name: '<unknown member>' })
    }), [members, memberIds]);
}

export function useChannels(channelIds: string[]): { channels?: Room[], error?: Error } {
    const { channels } = useOrganizationData();
    return useMemo(() => ({
        channels: channelIds.map(channelId =>
            channels.data?.find(c => c.platformRoomId === channelId)
            ?? { platformRoomId: channelId, name: '<unknown channel>' }),
        error: channels.error,
    }), [channels, channelIds]);
}

const roomsFetchUrl = '/api/internal/rooms';
const membersFetchUrl = '/api/internal/members/active';
export function OrganizationDataContextProvider(props: PropsWithChildren<object>) {
    const channels = useFetch<Room[]>(roomsFetchUrl);
    const members = useFetch<Member[]>(membersFetchUrl);

    const value = useMemo(() => ({
        channels,
        members,
    }), [channels, members]);

    if (channels.error) {
        appInsights.trackException({ exception: members.error }, { component: 'OrganizationDataContextProvider', fetchUrl: roomsFetchUrl });
        log.error("Error loading members", { error: members.error });
    }

    if (members.error) {
        appInsights.trackException({ exception: members.error }, { component: 'OrganizationDataContextProvider', fetchUrl: membersFetchUrl });
        log.error("Error loading members", { error: members.error });
    }

    return (
        <OrganizationDataContext.Provider value={value}>
            {props.children}
        </OrganizationDataContext.Provider>
    );
}