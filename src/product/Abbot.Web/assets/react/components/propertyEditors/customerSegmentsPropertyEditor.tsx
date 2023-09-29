import {PropertyEditor, PropertyLabel, StepPropertyProps} from "./propertyEditor";
import {Option} from "../../models/options";
import {SegmentsDropDown} from "./segmentsDropDown";
import * as React from "react";
import {Tooltip} from "react-tooltip";
import {useEffect} from "react";

export function CustomerSegmentsPropertyEditor({property, onChange, value}: StepPropertyProps<Option[]>) {
    useEffect(() => {
            if (!value) {
                onChange([]);
            }
        },
        [value, onChange]);

    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}
                           className="flex flex-col"
                           data-tooltip-id="cs-pe-tooltip"
                           data-tooltip-content="Select segments to filter customers by. Leave blank for all customers.">
            </PropertyLabel>
            <SegmentsDropDown
                onChange={onChange}
                placeholder="All customers…"
                required={!!property.required}
                value={value as Option[]} />
            <Tooltip id="cs-pe-tooltip" />
        </PropertyEditor>
    );
}