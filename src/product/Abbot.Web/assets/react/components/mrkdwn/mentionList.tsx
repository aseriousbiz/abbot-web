import React, {
    forwardRef, useEffect, useImperativeHandle,
    useState,
} from 'react'
import {Tooltip} from "react-tooltip";

export interface MentionItem {
    id: string;
    label: string;
    avatar?: string;
    emoji?: boolean,
}

export default forwardRef(function MentionList(props: { query: string, items: MentionItem[], command: (args: MentionItem) => void}, ref) {
    const [selectedIndex, setSelectedIndex] = useState(0)

    const selectItem = index => {
        const item = props.items[index]

        if (item) {
            props.command(item)
        }
    }

    const upHandler = () => {
        setSelectedIndex((selectedIndex + props.items.length - 1) % props.items.length)
    }

    const downHandler = () => {
        setSelectedIndex((selectedIndex + 1) % props.items.length)
    }

    const enterHandler = () => {
        selectItem(selectedIndex)
    }

    useEffect(() => setSelectedIndex(0), [props.items])

    useImperativeHandle(ref, () => ({
        onKeyDown: ({ event }) => {
            if (event.key === 'ArrowUp') {
                upHandler()
                return true
            }

            if (event.key === 'ArrowDown') {
                downHandler()
                return true
            }

            if (event.key === 'Enter') {
                enterHandler()
                return true
            }

            return false
        },
    }))

    const itemClass = "px-2 py-1 m-0 text-left w-full";

    const classSelector = (index) => {
        return index === selectedIndex
            ? 'border-b border-t-yellow-50 border-b-yellow-300 bg-yellow-200 text-black'
            : 'border-t-transparent border-b-transparent bg-transparent text-gray-700';
    }

    return (
        <>
            <div id="suggestion-list" className="bg-white rounded-lg border shadow overflow-hidden m-0 relative" role="listbox">
                {props.items.length
                    ? props.items.map((item, index) => (
                        <button
                            className={`${itemClass} ${item.id.startsWith('{') ? 'italic' : null} ${classSelector(index)} min-w-[400px] hover:bg-yellow-100 hover:text-black has-tooltip-arrow`}
                            key={index}
                            data-tooltip-id="tooltip-suggestions"
                            data-tooltip-content={item.label}
                            onClick={() => selectItem(index)}
                            role="option"
                        >
                            {renderAvatar(item)}
                            {item.emoji ? item.id : item.label}
                        </button>
                    ))
                    : (
                        <div className={`${itemClass} italic text-gray-600 text-center`}>
                            {props.query.length > 0 ? 'No results found' : 'Start typing to get results'}
                        </div>
                    )
                }
            </div>
            <Tooltip id="tooltip-suggestions" />
        </>
    )
})

function renderAvatar(item: MentionItem) {
    const avatar = item.emoji ? item.label : item.avatar;
    const label = item.emoji ? item.id : item.label;

    if (!avatar) {
        return null;
    }
    if (avatar.startsWith('http')) {
        return (
            <img src={avatar}
                 className="w-5 h-5 inline-block object-cover shrink-0 mr-2 border-1 border-white rounded-full"
                 title={label}
                 alt="" />
        )
    }
    return (
        <span className="w-5 h-5 inline-block shrink-0 mr-2 border-1 border-white rounded-full">{avatar}</span>
    );
}