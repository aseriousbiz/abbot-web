import * as React from "react";
import {createContext, useContext, useMemo, useState} from "react";

export interface OverlayState {
    overlay: boolean,
    setOverlay: (Overlay: boolean) => void,
}

export const OverlayContext = createContext(null as OverlayState | null);

export default function useOverlay() {
    const context = useContext(OverlayContext);
    if (!context) throw new Error("OverlayContext not found!");
    return context;
}

export function OverlayProvider(props: {children: React.ReactNode}) {
    const [overlay, setOverlay] = useState(false);

    const value = useMemo(() => ({
        overlay,
        setOverlay,
    }), [overlay]);

    return (
        <>
            <OverlayContext.Provider value={value}>
                { props.children }
            </OverlayContext.Provider>
            {overlay ? (
                <div className="absolute w-full h-full m-0 bg-gray-600 opacity-50 inset-0 grow border rounded-2xl z-10"></div>
            ) : null}
        </>
    );
}