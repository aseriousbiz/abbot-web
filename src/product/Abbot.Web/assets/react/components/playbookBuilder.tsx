import StepKindPanel from "./stepKindPanel";
import PropertiesPanel from "./propertiesPanel";
import TriggerBlocks from "./triggerBlocks";
import {ActionBlocks} from "./actionBlocks";
import AddActionButton from "./addActionButton";
import usePlaybook from "../hooks/usePlaybooks";
import { isStaffMode } from "../../ts/env";
import DebugPanel from "./debugPanel";
import { StepLocation } from "../models/step";
import {Tooltip} from "react-tooltip";
import React from "react";

export default function PlaybookBuilder() {
    const { playbook } = usePlaybook();

    const startLocation = new StepLocation(playbook.startSequence);

    return (
        <>
            { isStaffMode && <DebugPanel /> }
            <StepKindPanel kind={'Trigger'} className="fixed right-4 top-4 max-h-[calc(100%-24px)]" title="Add Trigger"/>
            <StepKindPanel kind={'Action'} className="fixed right-4 top-4 max-h-[calc(100%-24px)]" title="Add Step"/>
            <PropertiesPanel className="fixed right-4 top-4 max-h-[calc(100%-24px)]" title="Properties"/>
            <div className="grow overflow-auto h-full relative">
                <section className="absolute w-full grow items-center flex-1 flex flex-col gap-y-1 px-16 py-8">
                    <div className="flex flex-col items-center gap-y-2">
                        <TriggerBlocks/>
                        <ActionBlocks sequence={playbook.startSequence} />
                        <AddActionButton showLabel={true}
                                         location={startLocation} />
                    </div>
                </section>
            </div>
            <Tooltip id="global-tooltip" />
        </>
    );
}