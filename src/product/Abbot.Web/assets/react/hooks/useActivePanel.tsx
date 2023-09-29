import * as React from "react";
import {createContext, useContext, useMemo, useState} from "react";
import {PanelKind} from "../components/panel";
import useOverlay from "./useOverlay";
import logger from "../../ts/log";

const log = logger("useActivePanel");

export interface ActivePanelContextState {
    activePanel?: PanelKind,
    setActivePanel: (activePanel: PanelKind) => void,
}

export const ActivePanelContext = createContext(null as ActivePanelContextState | null);

export default function useActivePanel() {
    const context = useContext(ActivePanelContext);
    if (!context) throw new Error("ActivePanelContext not found!");
    return context;
}

export function ActivePanelContextProvider(props: {children: React.ReactNode}) {
    const [activePanel, setActivePanel] = useState('None' as PanelKind);
    const {setOverlay} = useOverlay();

    const value = useMemo(() => ({
        activePanel,
        setActivePanel: setActivePanelAndOverlay,
    }), [activePanel]);

    function setActivePanelAndOverlay(activePanel: PanelKind){
        log.verbose(`Setting active panel to ${activePanel}`);
        activePanel ||= 'None';
        setOverlay(activePanel !== 'None');
        setActivePanel(activePanel);
    }

    return (
        <ActivePanelContext.Provider value={value}>
            { props.children }
        </ActivePanelContext.Provider>
    );
}