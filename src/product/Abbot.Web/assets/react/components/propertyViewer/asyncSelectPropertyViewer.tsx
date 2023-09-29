import { useMemo } from "react";
import {PropertyViewerProps} from ".";
import {useChannels, useMembers} from "../../hooks/useOrganizationData";
import { AsyncSelectOption } from "../propertyEditors/asyncSelectPropertyEditor";
import DefaultValueViewer from "./defaultValueViewer";
import Icon from "../icon";
import usePlaybook from "../../hooks/usePlaybooks";
import {PredefinedExpressions} from "../../models/predefinedExpressions";
import {PlacedStep} from "../../models/step";

type AsyncSelectPropertyViewerProps = {
    items: AsyncSelectOption[],
    step?: PlacedStep,
};

function AsyncSelectPropertyViewer({ items, step }: AsyncSelectPropertyViewerProps) {
    const viewers = items.map(item => {
        // We don't always have 'isExpression' set, so we have to check the value.
        if (item.value.startsWith('{{') || item.isExpression) {
            return <DefaultValueViewer key={item.value} value={item.value} step={step} />
        }

        if (item.label === undefined || item.isLoading) {
            return <span key={item.value} className="contents">
                <Icon icon="fa fa-spinner fa-spin-pulse" />
                Loading…
            </span>
        }
        return <span key={item.value} className="contents">
            {item.image && <img src={item.image} className="w-6 h-6 rounded-full" alt="" />}
            {item.label}
        </span>;
    });

    return (
        <div className="flex flex-row items-center gap-2">
            {viewers}
        </div>
    );

}

export function ChannelPropertyViewer({ value, step }: PropertyViewerProps<string | string[]>) {
    const { playbook } = usePlaybook();
    const channelIds = Array.isArray(value) ? value : [value];
    const { channels } = useChannels(channelIds);

    const options = useMemo(() => channels.map(channel => (
        PredefinedExpressions.getExpressionOption(channel.platformRoomId, playbook, step)
        ?? {
            value: channel.platformRoomId,
            label: channel.name,
        })), [channels]);

    return <AsyncSelectPropertyViewer items={options} step={step} />;
}

export const MessageTargetPropertyViewer = ChannelPropertyViewer;

export function MemberPropertyViewer({ value, step }: PropertyViewerProps<string | string[]>) {
    const memberIds = Array.isArray(value) ? value : [value];
    const { members } = useMembers(memberIds);
    const options = useMemo(() => members.map(member => ({
        value: member.platformUserId,
        label: member.nickName,
        image: member.avatarUrl,
    })), [members]);

    return <AsyncSelectPropertyViewer items={options} step={step} />;
}