import * as React from "react";
import {createContext, useContext, useMemo, useState} from "react";

export interface ReadonlyContextState {
    readonly: boolean,
    setReadonly: (readonly: boolean) => void,
}

export const ReadonlyContext = createContext(null as ReadonlyContextState | null);

export default function useReadonly() {
    const context = useContext(ReadonlyContext);
    if (!context) throw new Error("ReadonlyContext not found!");
    return context;
}

export function ReadonlyProvider(props: {readonly: boolean, children: React.ReactNode}) {
    const [readonly, setReadonly] = useState(props.readonly);

    const value = useMemo(() => ({
        readonly,
        setReadonly,
    }), [readonly]);

    return (
        <ReadonlyContext.Provider value={value}>
            { props.children }
        </ReadonlyContext.Provider>
    );
}