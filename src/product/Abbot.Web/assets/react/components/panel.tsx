import useActivePanel from "../hooks/useActivePanel";
import {PropsWithChildren, useRef} from "react";
import usePlaybookActivity from "../hooks/usePlaybookActivity";
import { StepKind } from "../models/step";

export interface PlaybookPanelProps extends PanelProps {
    id: PanelKind,
}

export interface PanelProps {
    title: string,
    className: string,
}

export type PanelKind = 'None' | 'Properties' | StepKind;

export default function Panel({id, title, className, children} : PropsWithChildren<PlaybookPanelProps>) {
    const { activePanel } = useActivePanel();
    const { clearActionStack } = usePlaybookActivity();

    const panelRef = useRef<HTMLElement>();

    if (activePanel !== id) {
        return null;
    }

    return (
        <section ref={panelRef}
                 id={`${id}-panel`}
                 className={`p-2 border border-slate-300 bg-white rounded-xl flex flex-col w-80 shadow-md z-30 max-h-full ${className}`}>
            <header className="mx-1 mt-2 mb-4 flex items-center">
                <h2 className="font-semibold text-sm">
                    {title}
                </h2>
                <button onClick={clearActionStack /* Clicking the 'x' means you want the panel gone, so clear the stack */}
                        className="close-panel-button ml-auto mr-2 font-medium text-gray-500 hover:text-black">
                    <i className="fa-regular fa-times"></i>
                </button>
            </header>

            <div className="p-1 grow overflow-y-scroll">
                {children}
            </div>
        </section>
    );
}

