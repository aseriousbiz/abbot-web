import {PropertyEditor, PropertyLabel, StepPropertyProps} from "./propertyEditor";
import {DurationUnits, DurationValue} from "../../models/durationValue";
import React, { useEffect } from "react";
import {DropDown, TextInput} from "./formControls";
import { isStaffMode } from "../../../ts/env";

export default function DurationPropertyEditor({ property, value, onChange }: StepPropertyProps<string>) {
    const [duration, setDuration] = React.useState<DurationValue>(() => DurationValue.parse(value || "P1D"));
    useEffect(() => onChange(duration.toISOString()), [duration]);
    const id = `duration-editor-${Date.now()}`;

    // 800M = BOOM, get it?
    if (isStaffMode && duration.toISOString() === 'P66Y8M') { throw new Error('800M!'); }

    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property} htmlFor={id} />
            <div className="flex items-baseline gap-2">
                <TextInput type="number"
                    className="w-24"
                    id={id}
                    value={duration.magnitude}
                    required={!!property.required /* This can't really be empty in the current design, so this is somewhat moot */}
                    min={1}
                    onChange={v => {
                        // Enforce min value of 1, rounded to nearest integer, with NaN coalesced to 0
                        const value = Math.max(1, Math.round(parseFloat(v) || 0));
                        setDuration(d => d.withMagnitude(value));
                    }} />
                <DropDown
                    onChange={v => {
                        const unit = v as DurationUnits;
                        setDuration(d => d.withUnit(unit));
                    }}
                    value={duration.unit}>
                    {
                        Object.values(DurationUnits).map((value) => (
                            <option key={value} value={value}>{value}</option>
                        ))
                    }
                </DropDown>
            </div>
            {isStaffMode && (
                <TextInput
                    name={property.name}
                    value={duration.toISOString()}
                    onChange={isStaffMode && (v => setDuration(DurationValue.parse(v)))}
                />
            )}

        </PropertyEditor>
    );
}