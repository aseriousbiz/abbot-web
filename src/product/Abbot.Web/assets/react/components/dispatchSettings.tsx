import {DispatchType} from "../../ts/api/internal";
import useFeatureFlags from "../hooks/useFeatureFlags";
import usePlaybook from "../hooks/usePlaybooks"
import {allDispatchOptions} from "../models/dispatch";
import FeatureSparkle from "./featureSparkle";
import {useMemo} from "react";
import DispatchFilter from "./dispatchFilter";

export default function DispatchSettings() {
    const { playbook, setPlaybook, savePlaybook } = usePlaybook();
    const { hasFeature } = useFeatureFlags();
    
    if (!hasFeature("PlaybookDispatching")) {
        return null;
    }

    // We only show this if every trigger has more than one dispatch type.
    const showDispatch = useMemo(() => {
        return playbook.triggers.steps.length > 0
            && !playbook.triggers.steps.some(t => t.type.additionalDispatchTypes.length === 0);
    }, [playbook]);

    function handleDispatchTypeChanged(e: React.ChangeEvent<HTMLSelectElement>) {
        const newPlaybook = playbook.withDispatchSettings({
            type: e.target.value as DispatchType,
        })
        setPlaybook(newPlaybook);
        savePlaybook();
    }

    if (!showDispatch) {
        // If we're not showing dispatch settings, then we need to make sure the playbook
        // is set to "Once" dispatching.
        if (playbook.dispatchSettings && playbook.dispatchSettings.type !== "Once") {
            const newPlaybook = playbook.withDispatchSettings({
                type: 'Once',
            });
            setPlaybook(newPlaybook);
            savePlaybook();
        }
        return null;
    }

    return <>
        <div className="gap-y-1 pt-2 mt-1 rounded-lg border border-dashed border-slate-300 px-2 py-2 text-slate-500">
            <p className="text-xs text-slate-500 font-medium mb-1">
                Dispatching
                <FeatureSparkle flags="PlaybookDispatching" />
            </p>
            <div className="flex">
                <div className="flex-col">
                    <div className="form-select-wrapper">
                        <select className="form-select py-2" onChange={handleDispatchTypeChanged} value={playbook.dispatchSettings.type}>
                            {Object.entries(allDispatchOptions).map(([name, title]) => (
                                <option key={name} value={name}>{title}</option>
                            ))}
                        </select>
                    </div>
                </div>
                {playbook.dispatchSettings.type !== "Once" && (
                    <>
                        <div className="flex-col my-2 ml-1">
                         in
                        </div>
                        <div className="flex-col">
                            <DispatchFilter dispatchSettings={playbook.dispatchSettings} />
                        </div>
                    </>
                )}
            </div>
        </div>
    </>
}