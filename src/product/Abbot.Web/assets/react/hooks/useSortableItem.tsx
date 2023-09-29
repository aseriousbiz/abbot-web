import * as React from "react";
import {createContext, useContext, useMemo, useState} from "react";
import {SyntheticListenerMap} from "@dnd-kit/core/dist/hooks/utilities";

/*
* Within a <SortableItem />, this provides the sort Id so we can use
* control where the <DragHandle/> is placed.
*/

export interface SortableItemContextState {
    listeners: SyntheticListenerMap,
    setListeners: (listeners: SyntheticListenerMap) => void,
}

export const SortableItemContext = createContext(null as SortableItemContextState | null);

export default function useSortableItem() {
    const context = useContext(SortableItemContext);
    if (!context) throw new Error("SortableItemContext not found!");
    return context;
}

export function SortableItemProvider(props: {listeners: SyntheticListenerMap, children: React.ReactNode}) {
    const [listeners, setListeners] = useState(props.listeners);

    const value = useMemo(() => ({
        listeners,
        setListeners,
    }), [listeners]);

    return (
        <SortableItemContext.Provider value={value}>
            { props.children }
        </SortableItemContext.Provider>
    );
}