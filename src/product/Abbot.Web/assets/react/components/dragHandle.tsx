import * as React from "react";
import useSortableItem from "../hooks/useSortableItem";
import useReadonly from "../hooks/useReadonly";

export default function DragHandle(props: {className?: string, children: React.ReactNode}) {
    const {listeners} = useSortableItem();
    const {readonly} = useReadonly();

    if (readonly) {
        return null;
    }

    return (
        <div className={`${props.className || ''} hover:cursor-move`} {...listeners}>
            {props.children}
        </div>
    );
}