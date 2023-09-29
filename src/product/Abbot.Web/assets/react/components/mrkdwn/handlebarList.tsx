import {forwardRef, useEffect, useImperativeHandle, useState,} from 'react'
import {Tooltip} from "react-tooltip";
import {ExpressionOption} from "../../models/options";

export type HandlebarItem = {
    id: string,
    label: string,
    context?: string,
};

export default forwardRef(function HandlebarList(props: { items: ExpressionOption[], command: (args: HandlebarItem) => void }, ref) {
    const [selectedIndex, setSelectedIndex] = useState(0)

    const selectItem = (index: number) => {
        const item = props.items[index]

        if (item) {
            props.command({
                id: item.expression,
                label: item.label,
                // The goal here is to add context to the handlebar Mention.
                // Unfortunately, custom attributes in mentions do not get saved into the
                // TipTap document. I don't know how to get it to do this. But I hope we
                // figure it out later.
                context: item.context,
            });
        }
    }

    const upHandler = () => {
        setSelectedIndex((selectedIndex + props.items.length - 1) % props.items.length)
    }

    const downHandler = () => {
        setSelectedIndex((selectedIndex + 1) % props.items.length)
    }

    const commitHandler = () => {
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

            if (['Enter', '}', 'Tab'].includes(event.key)) {
                commitHandler()
                return true
            }

            return false
        },
    }))

    const itemClass = "px-2 py-1 m-0 text-left w-full";

    const classSelector = (index: number) => {
        return index === selectedIndex
            ? 'border-y border-t-yellow-50 border-b-yellow-300 bg-yellow-200 text-black'
            : 'border-y border-t-transparent border-b-transparent bg-transparent text-gray-700';
    }

    return (
        <>
            <div id="suggestion-list" className="bg-white rounded-lg border shadow overflow-hidden m-0 relative min-w-[200px]" role="listbox">
                {props.items.length
                    ? props.items.map((item, index) => (
                        <button
                            className={`${itemClass} ${classSelector(index)} hover:bg-yellow-100 hover:text-black`}
                            key={index}
                            onClick={() => selectItem(index)}
                            role="option"
                            data-tooltip-id="tooltip-handlebars"
                            data-tooltip-content={item.value?.length > 0 ? item.value : 'Type in a custom expression'}
                        >
                            <div className="ml-2">
                                {item.label?.length > 0 ? (
                                    <div className="text-sm text-slate-600">{item.label}</div>
                                )
                                : (
                                    <div className="text-sm text-slate-600 italic">Custom handlebars expression</div>
                                )}
                                <div className="text-xs text-slate-400">{item.context}</div>
                            </div>
                        </button>
                    ))
                    : <div className={itemClass}>No result</div>
                }
            </div>
            <Tooltip id="tooltip-handlebars" />
        </>
    )
})
