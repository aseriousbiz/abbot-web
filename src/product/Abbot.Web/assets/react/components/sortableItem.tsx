import React from 'react';
import {useSortable} from '@dnd-kit/sortable';
import {CSS} from '@dnd-kit/utilities';
import {SortableItemProvider} from "../hooks/useSortableItem";
import useReadonly from "../hooks/useReadonly";

export interface SortableItemProps {
    id: string, /* Sort key */
    className?: string,
}

/*
* Container for a sortable item. This is useful for simple cases where
* the entire item is draggable.
*/
export default function SortableItem({id, className, children}: React.PropsWithChildren<SortableItemProps>) {
    const {
        attributes,
        listeners,
        setNodeRef,
        transform,
        transition,
    } = useSortable({id: id});

    const {readonly} = useReadonly();

    const style = {
        transform: CSS.Translate.toString(transform),
        transition,
    };

    if (readonly) {
        /* We want to remove attributes and style if readonly is true, but keep everything else. */
        return (
            <div id={id}
                 className={className || null}>
                <SortableItemProvider listeners={listeners}>
                 {children}
                </SortableItemProvider>
            </div>
        );
    }

    return (
        <div id={id}
                className={className || null}
                ref={setNodeRef}
                style={style}
                {...attributes}>
            <SortableItemProvider listeners={listeners}>
                {children}
            </SortableItemProvider>
        </div>
    );
}