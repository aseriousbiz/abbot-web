import {DispatchSettings} from "../models/step";
import * as React from "react";
import {Option} from "../models/options";
import {SegmentsDropDown} from "./propertyEditors/segmentsDropDown";
import usePlaybook from "../hooks/usePlaybooks";
import useFeatureFlags from "../hooks/useFeatureFlags";

export default function DispatchFilter(props: { dispatchSettings: DispatchSettings }) {
    const { playbook, setPlaybook, savePlaybook } = usePlaybook();
    const { hasFeature } = useFeatureFlags();

    if (!hasFeature("PlaybookDispatching")) {
        return null;
    }
    const dispatchSettings = props.dispatchSettings;

    const filterSegments = dispatchSettings.customerSegments ?? [];

    const filterSegmentOptions = filterSegments.map(segment => ({ label: segment, value: segment }) as Option);

    function handleSegmentChange(options: Option[]) {
        const newPlaybook = playbook.withDispatchSettings({
            type: dispatchSettings.type,
            customerSegments: options.map(o => o.value as string),
        });
        setPlaybook(newPlaybook);
        savePlaybook();
    }

    return (
        <div className="ml-2 text-xs">
            <SegmentsDropDown
                onChange={handleSegmentChange}
                placeholder="All customer segments"
                required={false}
                value={filterSegmentOptions} />
        </div>
    );
}