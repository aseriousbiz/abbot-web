import {MouseEventHandler, PropsWithChildren, useState} from "react";
import * as React from "react";
import {Tooltip} from "react-tooltip";

interface MenuButtonProps {
    onClick: MouseEventHandler<HTMLButtonElement>,
    isActive: boolean,
    disabled: boolean,
    tooltip?: string,
}

/*
 * This is a button within the Menu Bar for a rich text editor.
 */
export function MenuButton({onClick, isActive, disabled, tooltip, children}: PropsWithChildren<MenuButtonProps>) {
    const [hovered, setHovered] = useState(false); 
    const toggleHover = () =>  setHovered(!hovered);
    
    return (
        <div>
            <Tooltip id="mrkdwn-style-tooltip" />
            <button
                type="button"
                onClick={onClick}
                disabled={disabled}
                onMouseEnter={toggleHover}
                onMouseLeave={toggleHover}
                data-tooltip-id="mrkdwn-style-tooltip"
                data-tooltip-content={tooltip}
                className={`rounded-sm p-1 font-medium inline-flex justify-center items-center text-indigo-500 w-6 h-6 ${hovered ? "bg-indigo-50" : "" } ${isActive ? 'bg-indigo-100' : ''}`}>
                {children}
            </button>
        </div>
    )
}
