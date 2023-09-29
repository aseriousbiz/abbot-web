import * as React from "react";
import {createContext, useContext, useMemo} from "react";
import { isStaffMode } from "../../ts/env";
import { useLocalStorage } from "usehooks-ts";

export interface DebugContextState {
    debug: boolean,
    setDebug: (debug: boolean) => void,
}

export const DebugContext = createContext(null as DebugContextState | null);

export default function useDebug() {
    const context = useContext(DebugContext);
    if (!context) throw new Error("DebugContext not found!");
    return context;
}

export function DebugContextProvider(props: {children: React.ReactNode}) {
    const [debugEnabled, setDebugEnabled] = useLocalStorage('abbot:debug', false);

    const value = useMemo(() => ({
        get debug() {
            return isStaffMode && debugEnabled;
        },
        setDebug: setDebugEnabled,
    }), [debugEnabled]);

    return (
        <DebugContext.Provider value={value}>
            { props.children }
        </DebugContext.Provider>
    );
}